using System.Globalization;
using System.Text.Json;
using Cloud.Shared;
using Cloud.Shared.Auth;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Cloud.Shared.FuelAnalysis;
using Cloud.Shared.Streaming;
using Common;
using Common.FuelAnalysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.FuelAnalysis;

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
    public async Task<ActionResult<FuelRangeSnapshot>> LoadFuelSnapshotAsync(int teamId, string carNumber, int raceId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        // Prefer ChannelProcessor's telemetry-backed snapshot when it matches the requested
        // race. The Redis key is team+car, so a snapshot can survive a UI race switch and
        // still describe the *previous* race — serving that as-is is the "3,021 min ago"
        // bug. When it doesn't match, fall through to an offline PitFill build from DB so
        // engineer-managed flows (manual stints + entered fuel volumes) still produce a
        // useful range without live telemetry.
        var carKey = string.Format(Consts.CAR_STREAM_FIELD, teamId, carNumber);
        var cached = await snapshotStore.GetAsync(carKey, ct);
        if (cached is not null && cached.RaceId == raceId)
        {
            return cached;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var synthetic = await OfflineSnapshotBuilder.BuildAsync(db, teamId, carNumber, raceId, DateTime.UtcNow, ct);
        return synthetic is null ? NotFound() : synthetic;
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
        // EF Core can't translate DateTime arithmetic to SQL, so fetch the most-recently-started
        // race that started at-or-before atUtc and check the end boundary in memory.
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var candidate = await db.Races.AsNoTracking()
            .Where(r => r.TeamId == teamId && r.Start <= atUtc)
            .OrderByDescending(r => r.Start)
            .Select(r => new { r.Start, r.Duration })
            .FirstOrDefaultAsync(ct);
        var raceExists = candidate is not null && atUtc < candidate.Start.AddHours(candidate.Duration);
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
            .FirstOrDefaultAsync(r => r.Id == refuelEventId && r.TeamId == teamId, ct);
        if (existing is null) { return NotFound(); }

        // Preferred path: publish through the telemetry stream so ManualEntryHandler does the
        // DB write and refreshes CarFuelState. When the car has no live CarGateway connection
        // (e.g. manually-created stints in an offline race), publish returns 0 — fall back to
        // writing the volume directly on the existing RefuelEvent row, mirroring
        // ManualEntryHandler.UpdateVolumeOnExistingAsync.
        var published = await PublishManualFuelAsync(teamId, existing.CarNumber, request.Gallons, existing.DetectedAt, ct);
        if (published == 0)
        {
            existing.EnteredFuelGallons = request.Gallons;
            existing.EnteredAt = existing.DetectedAt;
            existing.Status = "Confirmed";
            await db.SaveChangesAsync(ct);
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
    [ProducesResponseType<CarFuelConfig>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarFuelConfig>> LoadCarFuelConfigAsync(int teamId, string carNumber, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        // Read just the active CarConfiguration's fuel-slice. Returning the entire
        // CarConfiguration here would be wasteful — the Fuel & Pit Strategy UI only
        // needs TankCapacityGallons / DefaultConsumptionGalPerMin to project future stints.
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var json = await db.CarConfigurations
            .AsNoTracking()
            .Where(c => c.TeamId == teamId && c.Car == carNumber)
            .OrderByDescending(c => c.LastUpdated)
            .Select(c => c.ConfigurationJson)
            .FirstOrDefaultAsync(ct);

        if (json is null) return NotFound();
        try
        {
            var config = JsonSerializer.Deserialize<CarConfiguration>(json);
            if (config?.FuelConfig is null) return NotFound();
            return config.FuelConfig;
        }
        catch (JsonException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    [ProducesResponseType<List<Stint>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<Stint>>> LoadStintsAsync(int teamId, string carNumber, int raceId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Lazy-create the next Auto placeholder if the latest stint's expected EndAt has
        // elapsed in real time. Stint times are naive race-TZ wall-clock in the DB, so
        // "now" must be in the same frame before comparing.
        var raceTz = await db.Races.AsNoTracking()
            .Where(r => r.Id == raceId && r.TeamId == teamId)
            .Select(r => r.TimeZone)
            .FirstOrDefaultAsync(ct);
        var nowInRaceTz = OfflineSnapshotBuilder.ConvertUtcToNaiveRaceTz(DateTime.UtcNow, raceTz);
        await StintEditor.EnsureLatestStintFollowOnIfEndedAsync(db, teamId, carNumber, raceId, nowInRaceTz, ct);

        return await db.Stints
            .AsNoTracking()
            .Where(s => s.TeamId == teamId && s.CarNumber == carNumber && s.RaceId == raceId)
            .OrderBy(s => s.StartAt)
            .ToListAsync(ct);
    }

    #endregion

    #region Stint editor (Add/Edit/Delete from Fuel & Pit Strategy UI)

    [HttpPost]
    [ProducesResponseType<Stint>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Stint>> SaveStintAsync(int teamId, [FromBody] CreateStintRequest request, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        var startUtc = request.StartAt.ToUniversalTime();
        var endUtc = request.EndAt?.ToUniversalTime();

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var outcome = await StintEditor.CreateAsync(db, teamId, request.CarNumber, request.RaceId,
            startUtc, endUtc, request.OriginType, ct);

        if (outcome.Error is StintEditor.CreateError err)
        {
            return err switch
            {
                StintEditor.CreateError.CarNotFound => NotFound("Car not found for this team."),
                StintEditor.CreateError.RaceNotFound => NotFound("Race not found for this team."),
                StintEditor.CreateError.EndBeforeStart => ValidationProblem("End time must be after start time."),
                _ => StatusCode(StatusCodes.Status500InternalServerError),
            };
        }
        return outcome.Success!.Stint;
    }

    [HttpPost]
    [ProducesResponseType<Stint>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Stint>> UpdateStintAsync(int teamId, int stintId, [FromBody] UpdateStintRequest request, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        var startUtc = request.StartAt.ToUniversalTime();
        var endUtc = request.EndAt?.ToUniversalTime();

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var outcome = await StintEditor.UpdateAsync(db, teamId, stintId,
            startUtc, endUtc, request.OriginType, request.NoFuelOnPit, ct);

        if (outcome.Error is StintEditor.UpdateError err)
        {
            return err switch
            {
                StintEditor.UpdateError.StintNotFound => NotFound(),
                StintEditor.UpdateError.EndBeforeStart => ValidationProblem("End time must be after start time."),
                StintEditor.UpdateError.NoPriorWindowToMerge => ValidationProblem("Cannot mark as 'No fuel on pit' — this is the first stint and there is no prior fuel window to merge into."),
                StintEditor.UpdateError.NoFollowingWindowToSplitFrom => ValidationProblem("Cannot split a stint that is not currently inside another fuel window."),
                _ => StatusCode(StatusCodes.Status500InternalServerError),
            };
        }
        return outcome.Success!;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStintAsync(int teamId, int stintId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var outcome = await StintEditor.DeleteAsync(db, teamId, stintId, ct);

        if (outcome.Error is StintEditor.DeleteError err)
        {
            return err switch
            {
                StintEditor.DeleteError.StintNotFound => NotFound(),
                StintEditor.DeleteError.OwnsFirstWindowAndHasFollowers => ValidationProblem("Cannot delete the first stint of the race when later stints exist in the same fuel window — delete the followers first."),
                _ => StatusCode(StatusCodes.Status500InternalServerError),
            };
        }
        return NoContent();
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
public record CreateStintRequest(string CarNumber, int RaceId, DateTime StartAt, DateTime? EndAt, StintOriginType OriginType);
public record UpdateStintRequest(DateTime StartAt, DateTime? EndAt, StintOriginType OriginType, bool NoFuelOnPit);
