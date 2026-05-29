using ChannelProcessor.FuelAnalysis.ChannelInput;
using ChannelProcessor.FuelAnalysis.State;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Microsoft.EntityFrameworkCore;

namespace ChannelProcessor.FuelAnalysis.Windows;

/// <summary>
/// Drives Stint boundaries off <c>InPit</c> transitions. A Stint represents continuous
/// on-track running between pit-in events and is independent of <see cref="FuelWindow"/>
/// boundaries — a tire-only pit stop closes a Stint without opening a new FuelWindow.
/// Also shortens the ECU reset classification deadline on pit-out per design.md §844.
/// </summary>
public sealed class StintLifecycle(ILogger<StintLifecycle> logger)
{
    public async Task<CarFuelState> ApplyAsync(
        RaceManagementContext db,
        int teamId, string carNumber, int raceId,
        CarFuelState state,
        FuelInputs inputs,
        CancellationToken ct)
    {
        if (inputs.InPit is not TimestampedBool incoming) return state;

        // Drop out-of-order updates — keep state monotonic by timestamp.
        if (state.LastInPitTimestamp is DateTime last && incoming.TimestampUtc <= last)
            return state;

        var was = state.LastInPitValue;
        var now = incoming.Value;

        state.LastInPitValue = now;
        state.LastInPitTimestamp = incoming.TimestampUtc;

        if (was == now) return state; // no edge

        // Flag the transition for the pace tracker — any lap completion seen between this
        // edge and the next valid lap should be excluded as in/out lap.
        state.InPitTransitionedSinceLastLap = true;

        if (!was && now)
        {
            // Pit-in: close current Stint
            if (state.OpenStintId is int openStintId)
            {
                var stint = await db.Stints.FirstOrDefaultAsync(s => s.Id == openStintId, ct);
                if (stint is not null && stint.EndAt is null)
                {
                    stint.EndAt = incoming.TimestampUtc;
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation(
                        "Closed Stint {StintId} on pit-in for team {TeamId} car {CarNumber}",
                        openStintId, teamId, carNumber);
                }
                state.OpenStintId = null;
            }
            return state;
        }

        // Pit-out: open a new Stint in the current FuelWindow
        if (state.OpenFuelWindowId is int currentWindowId)
        {
            var stint = new Stint
            {
                TeamId = teamId,
                CarNumber = carNumber,
                RaceId = raceId,
                FuelWindowId = currentWindowId,
                StartAt = incoming.TimestampUtc,
                OriginType = StintOriginType.Auto,
            };
            db.Stints.Add(stint);
            await db.SaveChangesAsync(ct);
            state.OpenStintId = stint.Id;
            state.MostRecentStintStartedAt = incoming.TimestampUtc;
            logger.LogInformation(
                "Opened Stint {StintId} on pit-out for team {TeamId} car {CarNumber} (window {WindowId})",
                stint.Id, teamId, carNumber, currentWindowId);
        }

        // Pit-out shortens the ECU reset classification deadline (design §844: "60s OR pit-out, whichever came first").
        if (state.CurrentWindowResetClassifyDeadline is DateTime deadline
            && incoming.TimestampUtc < deadline
            && !state.CurrentWindowResetClassified)
        {
            state.CurrentWindowResetClassifyDeadline = incoming.TimestampUtc;
        }

        return state;
    }
}
