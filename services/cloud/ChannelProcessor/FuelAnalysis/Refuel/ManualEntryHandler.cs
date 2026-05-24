using ChannelProcessor.FuelAnalysis.ChannelInput;
using ChannelProcessor.FuelAnalysis.State;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Microsoft.EntityFrameworkCore;

namespace ChannelProcessor.FuelAnalysis.Refuel;

/// <summary>
/// Processes <c>ManualFuelAddedGallons</c> values published by WebApi per ADR-0005.
/// <para>
/// The value's timestamp drives which kind of update it is:
/// </para>
/// <list type="bullet">
///   <item>If a <see cref="RefuelEvent"/> already exists with a near-matching
///     <see cref="RefuelEvent.DetectedAt"/> — engineer is entering the volume for an
///     existing event (auto-detected or just-created manual). Update
///     <see cref="RefuelEvent.EnteredFuelGallons"/> and mark it Confirmed.</item>
///   <item>Otherwise — engineer is recording a new manual refuel from scratch. Insert
///     a manual <see cref="RefuelEvent"/>, close the open <see cref="FuelWindow"/>, open
///     a new one.</item>
/// </list>
/// State updates: <see cref="CarFuelState.CurrentWindowEnteredFuelGallons"/> is refreshed
/// whenever the update targets the open window's start event, and
/// <see cref="CarFuelState.ForceNextSnapshot"/> is set so PitFill picks up the new entry
/// without waiting for the 1-min cadence.
/// </summary>
public sealed class ManualEntryHandler(ILogger<ManualEntryHandler> logger)
{
    /// <summary>Timestamp tolerance for matching an inbound publish to an existing event — generous to absorb clock skew between WebApi and ChannelProcessor.</summary>
    private static readonly TimeSpan MatchTolerance = TimeSpan.FromSeconds(15);

    public async Task<CarFuelState> ApplyAsync(
        RaceManagementContext db,
        int teamId, string carNumber, int raceId,
        CarFuelState state,
        FuelInputs inputs,
        CancellationToken ct)
    {
        if (inputs.ManualFuelAddedGallons is not TimestampedDouble entry) return state;
        if (entry.Value <= 0)
        {
            logger.LogWarning(
                "Ignoring ManualFuelAddedGallons of {Value} gal at {At} (must be > 0) for team {TeamId} car {CarNumber}",
                entry.Value, entry.TimestampUtc, teamId, carNumber);
            return state;
        }

        var lo = entry.TimestampUtc - MatchTolerance;
        var hi = entry.TimestampUtc + MatchTolerance;
        // Within a 15s tolerance window, expect at most one match in practice. Order by Id
        // to pick the earliest-inserted on the off chance two refuels truly are bundled into
        // the same instant.
        var existing = await db.RefuelEvents
            .Where(r => r.TeamId == teamId && r.CarNumber == carNumber && r.RaceId == raceId
                        && r.DetectedAt >= lo && r.DetectedAt <= hi)
            .OrderBy(r => r.Id)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            return await UpdateVolumeOnExistingAsync(db, state, existing, entry, ct);
        }

        return await CreateNewManualEventAsync(db, teamId, carNumber, raceId, state, entry, ct);
    }

    private async Task<CarFuelState> UpdateVolumeOnExistingAsync(
        RaceManagementContext db, CarFuelState state, RefuelEvent existing, TimestampedDouble entry, CancellationToken ct)
    {
        // Idempotent — if the value already matches, skip the write (re-process of the same stream message).
        if (existing.EnteredFuelGallons is double current && Math.Abs(current - entry.Value) < 0.0001)
        {
            return state;
        }

        existing.EnteredFuelGallons = entry.Value;
        existing.EnteredAt = entry.TimestampUtc;
        existing.Status = "Confirmed";
        await db.SaveChangesAsync(ct);

        if (state.OpenFuelWindowStartRefuelEventId == existing.Id)
        {
            state.CurrentWindowEnteredFuelGallons = entry.Value;
            state.ForceNextSnapshot = true;
        }

        logger.LogInformation(
            "Recorded {Gallons:F2} gal on RefuelEvent {EventId} (source {Source}, detected {DetectedAt})",
            entry.Value, existing.Id, existing.Source, existing.DetectedAt);
        return state;
    }

    private async Task<CarFuelState> CreateNewManualEventAsync(
        RaceManagementContext db,
        int teamId, string carNumber, int raceId,
        CarFuelState state, TimestampedDouble entry, CancellationToken ct)
    {
        var ev = new RefuelEvent
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            DetectedAt = entry.TimestampUtc,
            EnteredFuelGallons = entry.Value,
            EnteredAt = entry.TimestampUtc,
            ConfidenceTier = RefuelConfidenceTier.Manual,
            AnchorFlags = 0,
            Source = RefuelSource.Manual,
            EcuResetState = EcuResetState.Unknown,
            Status = "Confirmed",
        };
        db.RefuelEvents.Add(ev);
        await db.SaveChangesAsync(ct);

        if (state.OpenFuelWindowId is int openWindowId)
        {
            var oldWindow = await db.FuelWindows.FirstOrDefaultAsync(w => w.Id == openWindowId, ct);
            if (oldWindow is not null && oldWindow.ClosedAt is null)
            {
                oldWindow.ClosedAt = entry.TimestampUtc;
                oldWindow.EndRefuelEventId = ev.Id;
                if (oldWindow.OpenedAt < entry.TimestampUtc)
                    oldWindow.ObservedDurationSeconds = (entry.TimestampUtc - oldWindow.OpenedAt).TotalSeconds;
                await db.SaveChangesAsync(ct);
            }
        }

        var newWindow = new FuelWindow
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            StartRefuelEventId = ev.Id,
            OpenedAt = entry.TimestampUtc,
        };
        db.FuelWindows.Add(newWindow);
        await db.SaveChangesAsync(ct);

        state.OpenFuelWindowId = newWindow.Id;
        state.OpenFuelWindowStartRefuelEventId = ev.Id;
        state.OpenFuelWindowOpenedAt = entry.TimestampUtc;
        state.CurrentWindowFuelFullAssertedAt = null;
        state.CurrentWindowTripFuelDroppedAt = null;
        state.CurrentWindowResetClassifyDeadline = entry.TimestampUtc + TimeSpan.FromSeconds(60);
        state.CurrentWindowResetClassified = false;
        state.CurrentWindowEcuResetState = EcuResetState.Unknown;
        state.CurrentWindowEnteredFuelGallons = entry.Value;
        state.CurrentWindowFlowMeterFuelUsedAtOpen = state.LastFuelUsedValue;
        state.CurrentWindowThrottleProxyFuelUsedAtOpen = state.LastThrottleProxyFuelUsedValue;
        state.CurrentWindowCloudIntegratedFuelUsedAtOpen = state.CloudIntegratedFuelUsedGallons;
        state.ForceNextSnapshot = true;

        logger.LogInformation(
            "Opened manual RefuelEvent {EventId} + FuelWindow {WindowId} for team {TeamId} car {CarNumber}: {Gallons:F2} gal at {At}",
            ev.Id, newWindow.Id, teamId, carNumber, entry.Value, entry.TimestampUtc);
        return state;
    }
}
