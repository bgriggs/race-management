using ChannelProcessor.FuelAnalysis.ChannelInput;
using ChannelProcessor.FuelAnalysis.State;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Microsoft.EntityFrameworkCore;

namespace ChannelProcessor.FuelAnalysis.Session;

/// <summary>
/// Brings per-car fuel state up to live for an active race — either by rehydrating from
/// Postgres (if a previous pod created the open <see cref="FuelWindow"/>) or, on a true
/// session start, by writing the synthetic <see cref="RefuelSource.SessionStart"/>
/// <see cref="RefuelEvent"/> plus its initial <see cref="FuelWindow"/> and (when the car
/// is not currently in the pit) the first <see cref="Stint"/>.
/// </summary>
public sealed class SessionLifecycleHandler(ILogger<SessionLifecycleHandler> logger)
{
    public async Task<CarFuelState> EnsureInitializedAsync(
        RaceManagementContext db,
        int teamId, string carNumber, int raceId,
        CarFuelState? existing,
        FuelInputs firstInputs,
        DateTime firstMessageTimestampUtc,
        CancellationToken ct)
    {
        if (existing is not null && existing.RaceId == raceId && existing.OpenFuelWindowId is not null)
            return existing;

        // Try to rehydrate from a still-open window (pod restart / race reactivation case)
        var openWindow = await db.FuelWindows
            .AsNoTracking()
            .Where(w => w.TeamId == teamId && w.CarNumber == carNumber && w.RaceId == raceId && w.ClosedAt == null)
            .FirstOrDefaultAsync(ct);

        if (openWindow is not null)
        {
            var openStint = await db.Stints
                .AsNoTracking()
                .Where(s => s.FuelWindowId == openWindow.Id && s.EndAt == null)
                .OrderByDescending(s => s.StartAt)
                .FirstOrDefaultAsync(ct);
            var mostRecentStint = openStint ?? await db.Stints
                .AsNoTracking()
                .Where(s => s.FuelWindowId == openWindow.Id)
                .OrderByDescending(s => s.StartAt)
                .FirstOrDefaultAsync(ct);

            var startEvent = await db.RefuelEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == openWindow.StartRefuelEventId, ct);

            logger.LogInformation(
                "Rehydrated fuel state for team {TeamId} car {CarNumber} race {RaceId}: window {WindowId}, stint {StintId}",
                teamId, carNumber, raceId, openWindow.Id, openStint?.Id);

            return new CarFuelState
            {
                RaceId = raceId,
                OpenFuelWindowId = openWindow.Id,
                OpenFuelWindowStartRefuelEventId = openWindow.StartRefuelEventId,
                OpenFuelWindowOpenedAt = openWindow.OpenedAt,
                OpenStintId = openStint?.Id,
                MostRecentStintStartedAt = mostRecentStint?.StartAt,
                CurrentWindowEcuResetState = startEvent?.EcuResetState ?? EcuResetState.Unknown,
                CurrentWindowEnteredFuelGallons = startEvent?.EnteredFuelGallons,
                CurrentWindowFlowMeterFuelUsedAtOpen = firstInputs.FuelUsed?.Value, // best-effort — true baseline lost on restart
                CurrentWindowThrottleProxyFuelUsedAtOpen = firstInputs.ThrottleProxyFuelUsed?.Value, // best-effort
                // Cloud rate-integral baseline is intentionally null on rehydrate — the grid
                // estimator stays unavailable until the next real FuelWindow opens.
                CurrentWindowCloudIntegratedFuelUsedAtOpen = null,
                // No incoming snapshot trigger on rehydrate; let the 1-min cadence drive the next emit.
            };
        }

        // Genuine session start
        var inPitAtStart = firstInputs.InPit?.Value ?? false;
        // Per design.md §848: SessionStart's ECU reset is Confirmed when TripFuel < 2 gal on first reading.
        var sessionStartReset = firstInputs.TripFuel?.Value < 2.0
            ? EcuResetState.ResetConfirmed
            : EcuResetState.Unknown;

        var sessionStartEvent = new RefuelEvent
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            DetectedAt = firstMessageTimestampUtc,
            EnteredFuelGallons = null,
            ConfidenceTier = RefuelConfidenceTier.Manual,
            AnchorFlags = 0,
            Source = RefuelSource.SessionStart,
            EcuResetState = sessionStartReset,
            Status = "Pending",
        };
        db.RefuelEvents.Add(sessionStartEvent);
        await db.SaveChangesAsync(ct);

        var window = new FuelWindow
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            StartRefuelEventId = sessionStartEvent.Id,
            OpenedAt = firstMessageTimestampUtc,
        };
        db.FuelWindows.Add(window);
        await db.SaveChangesAsync(ct);

        int? stintId = null;
        DateTime? stintStart = null;
        if (!inPitAtStart)
        {
            var stint = new Stint
            {
                TeamId = teamId,
                CarNumber = carNumber,
                RaceId = raceId,
                FuelWindowId = window.Id,
                StartAt = firstMessageTimestampUtc,
            };
            db.Stints.Add(stint);
            await db.SaveChangesAsync(ct);
            stintId = stint.Id;
            stintStart = firstMessageTimestampUtc;
        }

        logger.LogInformation(
            "Fuel session started for team {TeamId} car {CarNumber} race {RaceId}: SessionStart event {EventId}, window {WindowId}, ECU reset state {Reset}, inPit={InPit}",
            teamId, carNumber, raceId, sessionStartEvent.Id, window.Id, sessionStartReset, inPitAtStart);

        return new CarFuelState
        {
            RaceId = raceId,
            OpenFuelWindowId = window.Id,
            OpenFuelWindowStartRefuelEventId = sessionStartEvent.Id,
            OpenFuelWindowOpenedAt = firstMessageTimestampUtc,
            OpenStintId = stintId,
            MostRecentStintStartedAt = stintStart,
            LastInPitValue = inPitAtStart,
            LastInPitTimestamp = firstInputs.InPit?.TimestampUtc,
            CurrentWindowEcuResetState = sessionStartReset,
            CurrentWindowEnteredFuelGallons = null,
            CurrentWindowFlowMeterFuelUsedAtOpen = firstInputs.FuelUsed?.Value,
            CurrentWindowThrottleProxyFuelUsedAtOpen = firstInputs.ThrottleProxyFuelUsed?.Value,
            // The cloud rate-integral has just been seeded (or about to be by the first
            // ThrottleProxyRate sample) so its baseline at "this session start" is 0.
            CurrentWindowCloudIntegratedFuelUsedAtOpen = 0,
            ForceNextSnapshot = true,
        };
    }
}
