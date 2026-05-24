using System.Globalization;
using Cloud.Shared;
using Cloud.Shared.Auth;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Cloud.Shared.FuelAnalysis;
using Cloud.Shared.Streaming;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Controllers;

/// <summary>
/// User-facing endpoints for the Fuel Analysis subsystem:
/// <list type="bullet">
///   <item>Reading the latest <see cref="FuelRangeSnapshot"/> for the Race Monitor detail panel.</item>
///   <item>Recording manual refuels and entering volumes for auto-detected events
///     (both flow through the telemetry stream as <c>ManualFuelAddedGallons</c> per ADR-0005;
///     ChannelProcessor's <c>ManualEntryHandler</c> does the DB writes).</item>
///   <item>Reading the per-race <see cref="RefuelEvent"/> / <see cref="FuelWindow"/> /
///     <see cref="Stint"/> tables that back the Fuel &amp; Pit Strategy Gantt visualization.</item>
/// </list>
/// </summary>
[Route("v{version:apiVersion}/[controller]/[action]")]
[ApiVersion("1.0")]
[Produces("application/json")]
public class FuelController(
    ITeamRoleContext teamRoleContext,
    IDbContextFactory<RaceManagementContext> dbFactory,
    IFuelSnapshotStore snapshotStore,
    ICarChannelPublisher channelPublisher) : Controller
{
    // Reserved-channel id from Common/ReservedChannels.cs; duplicated as a const here so
    // the controller doesn't take a project ref on ChannelProcessor's internal GUID copies.
    private static readonly Guid ManualFuelAddedGallonsChannelId = Guid.Parse("e6c3f1a5-4d2b-4f8e-1c9a-3f5a7d2e4c03");

    private const double MinCalibrationFactor = 0.1;
    private const double MaxCalibrationFactor = 10.0;

    #region Snapshot

    [HttpGet]
    [ProducesResponseType<FuelRangeSnapshot>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FuelRangeSnapshot>> LoadFuelSnapshotAsync(int teamId, string carNumber, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        var carKey = string.Format(Consts.CAR_STREAM_FIELD, teamId, carNumber);
        var snapshot = await snapshotStore.GetAsync(carKey, ct);
        return snapshot is null ? NotFound() : snapshot;
    }

    #endregion

    #region Manual fuel entry

    [HttpPost]
    [ProducesResponseType<RefuelEventPublishResult>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveManualRefuelAsync(int teamId, string carNumber, [FromBody] ManualRefuelRequest request, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        if (request.Gallons <= 0)
        {
            ModelState.AddModelError(nameof(request.Gallons), "Gallons must be greater than zero.");
            return ValidationProblem(ModelState);
        }

        var atUtc = (request.AtUtc ?? DateTime.UtcNow).ToUniversalTime();

        // Validate there's a race covering this timestamp so the publish isn't dropped silently.
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var raceExists = await db.Races.AsNoTracking().AnyAsync(
            r => r.TeamId == teamId
                 && r.Start <= atUtc
                 && atUtc < r.Start.AddTicks((long)(r.Duration * TimeSpan.TicksPerHour)),
            ct);
        if (!raceExists)
        {
            ModelState.AddModelError(nameof(request.AtUtc), "No active race for the team covers this timestamp.");
            return ValidationProblem(ModelState);
        }

        var published = await PublishManualFuelAsync(teamId, carNumber, request.Gallons, atUtc, ct);
        if (published == 0)
        {
            return NotFound("Car has no active configuration or is not connected; cannot publish manual fuel entry.");
        }

        return Accepted(new RefuelEventPublishResult(atUtc, request.Gallons));
    }

    [HttpPost]
    [ProducesResponseType<RefuelEventPublishResult>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnterRefuelVolumeAsync(int teamId, int refuelEventId, [FromBody] EnterVolumeRequest request, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }
        if (request.Gallons <= 0)
        {
            ModelState.AddModelError(nameof(request.Gallons), "Gallons must be greater than zero.");
            return ValidationProblem(ModelState);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.RefuelEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == refuelEventId && r.TeamId == teamId, ct);
        if (existing is null) { return NotFound(); }

        // Publish at the event's original DetectedAt so ManualEntryHandler matches into it.
        var published = await PublishManualFuelAsync(teamId, existing.CarNumber, request.Gallons, existing.DetectedAt, ct);
        if (published == 0)
        {
            return NotFound("Car has no active configuration or is not connected; cannot publish manual fuel entry.");
        }

        return Accepted(new RefuelEventPublishResult(existing.DetectedAt, request.Gallons));
    }

    [HttpPost]
    [ProducesResponseType<RefuelEvent>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RefuelEvent>> DismissRefuelEventAsync(int teamId, int refuelEventId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.RefuelEvents
            .FirstOrDefaultAsync(r => r.Id == refuelEventId && r.TeamId == teamId, ct);
        if (existing is null) { return NotFound(); }

        // Idempotent: if already acknowledged or confirmed, return the row as-is.
        // PitFill loses calibration for this window — design.md §1010 says explicit data
        // loss is acceptable; the system never invents a volume.
        if (existing.Status == "Pending")
        {
            existing.Status = "Acknowledged-NoEntry";
            await db.SaveChangesAsync(ct);
        }

        return existing;
    }

    #endregion

    #region Calibration

    [HttpGet]
    [ProducesResponseType<CalibrationFactor>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalibrationFactor>> LoadCalibrationFactorAsync(int teamId, string carNumber, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var latest = await db.CalibrationFactors
            .AsNoTracking()
            .Where(c => c.TeamId == teamId && c.CarNumber == carNumber)
            .OrderByDescending(c => c.EffectiveAt)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct);

        return latest is null ? NotFound() : latest;
    }

    [HttpPost]
    [ProducesResponseType<CalibrationFactor>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalibrationFactor>> SaveCalibrationOverrideAsync(int teamId, string carNumber, [FromBody] CalibrationOverrideRequest request, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        if (request.Factor < MinCalibrationFactor || request.Factor > MaxCalibrationFactor)
        {
            ModelState.AddModelError(nameof(request.Factor),
                $"Factor must be between {MinCalibrationFactor:F2} and {MaxCalibrationFactor:F2}.");
            return ValidationProblem(ModelState);
        }

        return await InsertCalibrationRowAsync(teamId, carNumber, request.Factor, CalibrationFactorSource.ManualOverride, ct);
    }

    [HttpPost]
    [ProducesResponseType<CalibrationFactor>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalibrationFactor>> ResetCalibrationAsync(int teamId, string carNumber, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        return await InsertCalibrationRowAsync(teamId, carNumber, 1.0, CalibrationFactorSource.Reset, ct);
    }

    [HttpPost]
    [ProducesResponseType<CalibrationFactor>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CalibrationFactor>> ResumeCalibrationLearningAsync(int teamId, string carNumber, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var current = await db.CalibrationFactors
            .AsNoTracking()
            .Where(c => c.TeamId == teamId && c.CarNumber == carNumber)
            .OrderByDescending(c => c.EffectiveAt)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (current is null)
        {
            return BadRequest("No calibration history exists yet — nothing to resume from. Wait for a Reset-Confirmed FuelWindow to close, or set an override first.");
        }
        if (current.Source != CalibrationFactorSource.ManualOverride)
        {
            // Already learning — return current as a no-op for idempotency.
            return current;
        }

        // Carry forward the override value as the new EMA seed; flagging it Learned unblocks
        // the auto-learner on the next closed window.
        return await InsertCalibrationRowAsync(teamId, carNumber, current.Value, CalibrationFactorSource.Learned, ct);
    }

    #endregion

    #region Per-race history (for Gantt + breakdown)

    [HttpGet]
    [ProducesResponseType<List<RefuelEvent>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<RefuelEvent>>> LoadRefuelEventsAsync(int teamId, string carNumber, int raceId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.RefuelEvents
            .AsNoTracking()
            .Where(r => r.TeamId == teamId && r.CarNumber == carNumber && r.RaceId == raceId)
            .OrderBy(r => r.DetectedAt)
            .ToListAsync(ct);
    }

    [HttpGet]
    [ProducesResponseType<List<FuelWindow>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<FuelWindow>>> LoadFuelWindowsAsync(int teamId, string carNumber, int raceId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.FuelWindows
            .AsNoTracking()
            .Where(w => w.TeamId == teamId && w.CarNumber == carNumber && w.RaceId == raceId)
            .OrderBy(w => w.OpenedAt)
            .ToListAsync(ct);
    }

    [HttpGet]
    [ProducesResponseType<List<Stint>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<Stint>>> LoadStintsAsync(int teamId, string carNumber, int raceId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Stints
            .AsNoTracking()
            .Where(s => s.TeamId == teamId && s.CarNumber == carNumber && s.RaceId == raceId)
            .OrderBy(s => s.StartAt)
            .ToListAsync(ct);
    }

    #endregion

    private async Task<int> PublishManualFuelAsync(int teamId, string carNumber, double gallons, DateTime atUtc, CancellationToken ct)
    {
        var values = new List<PublishedChannelValue>
        {
            new(ManualFuelAddedGallonsChannelId, gallons.ToString("R", CultureInfo.InvariantCulture), atUtc),
        };
        return await channelPublisher.PublishAsync(teamId, carNumber, values, ct);
    }

    private async Task<CalibrationFactor> InsertCalibrationRowAsync(int teamId, string carNumber, double value, CalibrationFactorSource source, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = new CalibrationFactor
        {
            TeamId = teamId,
            CarNumber = carNumber,
            Value = value,
            Source = source,
            EffectiveAt = DateTime.UtcNow,
            // No RaceId on engineer-driven calibration changes — they happen out-of-band.
        };
        db.CalibrationFactors.Add(row);
        await db.SaveChangesAsync(ct);
        return row;
    }
}

public record ManualRefuelRequest(double Gallons, DateTime? AtUtc);
public record EnterVolumeRequest(double Gallons);
public record RefuelEventPublishResult(DateTime AtUtc, double Gallons);
public record CalibrationOverrideRequest(double Factor);
