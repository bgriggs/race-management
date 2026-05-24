using ChannelProcessor.FuelAnalysis.ChannelInput;
using ChannelProcessor.FuelAnalysis.State;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Microsoft.EntityFrameworkCore;

namespace ChannelProcessor.FuelAnalysis.Refuel;

/// <summary>
/// Classifies the ECU reset state (Confirmed / Inferred / Unreset) for the current open
/// <see cref="FuelWindow"/>'s opening Refuel Event per design.md §840–846. Tracks
/// <c>TripFuel</c> drops within the post-refuel window and commits the verdict on the
/// first message past <see cref="CarFuelState.CurrentWindowResetClassifyDeadline"/>
/// (either 60 s after the refuel or pit-out, whichever came first — set by
/// <see cref="Windows.FuelWindowLifecycle"/> and <see cref="Windows.StintLifecycle"/>).
/// </summary>
public sealed class EcuResetClassifier(ILogger<EcuResetClassifier> logger)
{
    private const double TripFuelDropThresholdGallons = 1.0;

    public async Task<CarFuelState> ProcessAsync(
        RaceManagementContext db,
        CarFuelState state,
        FuelInputs inputs,
        DateTime currentTimeUtc,
        CancellationToken ct)
    {
        if (state.CurrentWindowResetClassified) return state;
        if (state.CurrentWindowResetClassifyDeadline is not DateTime deadline) return state;
        if (state.OpenFuelWindowStartRefuelEventId is not int eventId) return state;

        // Record a TripFuel drop seen within the classification window.
        if (state.CurrentWindowTripFuelDroppedAt is null
            && inputs.TripFuel is TimestampedDouble tripFuel
            && tripFuel.Value < TripFuelDropThresholdGallons
            && tripFuel.TimestampUtc <= deadline)
        {
            state.CurrentWindowTripFuelDroppedAt = tripFuel.TimestampUtc;
        }

        if (currentTimeUtc < deadline) return state;

        var verdict =
            state.CurrentWindowFuelFullAssertedAt is not null
            && state.CurrentWindowTripFuelDroppedAt is not null
                ? EcuResetState.ResetConfirmed
            : state.CurrentWindowTripFuelDroppedAt is not null
                ? EcuResetState.ResetInferred
                : EcuResetState.Unreset;

        var refuelEvent = await db.RefuelEvents.FirstOrDefaultAsync(r => r.Id == eventId, ct);
        if (refuelEvent is not null && refuelEvent.EcuResetState != verdict)
        {
            refuelEvent.EcuResetState = verdict;
            await db.SaveChangesAsync(ct);
        }

        state.CurrentWindowResetClassified = true;
        state.CurrentWindowEcuResetState = verdict;
        // ECU estimator's availability flips on this verdict — force the next snapshot.
        state.ForceNextSnapshot = true;
        logger.LogInformation(
            "ECU reset verdict for RefuelEvent {EventId}: {Verdict} (FuelFull@{FuelFull}, TripFuelDrop@{Drop})",
            eventId, verdict, state.CurrentWindowFuelFullAssertedAt, state.CurrentWindowTripFuelDroppedAt);

        return state;
    }
}
