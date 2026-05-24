using System.Numerics;
using ChannelProcessor.FuelAnalysis.Calibration;
using ChannelProcessor.FuelAnalysis.State;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Microsoft.EntityFrameworkCore;

namespace ChannelProcessor.FuelAnalysis.Windows;

/// <summary>
/// Handles the create-new vs merge-with-existing decision for an anchor detection per
/// design.md §824–828: anchors agreeing within a 2-min window elevate the existing event's
/// <see cref="RefuelConfidenceTier"/>; an anchor outside that window opens a new
/// <see cref="RefuelEvent"/> and a new <see cref="FuelWindow"/>. On close, computes the
/// window's observed consumption rate and invokes <see cref="CalibrationFactorLearner"/>.
/// </summary>
public sealed class FuelWindowLifecycle(
    CalibrationFactorLearner calibrationLearner,
    ILogger<FuelWindowLifecycle> logger)
{
    private static readonly TimeSpan AnchorAgreementWindow = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan EcuResetClassifyDeadline = TimeSpan.FromSeconds(60);

    public async Task<CarFuelState> RecordAnchorAsync(
        RaceManagementContext db,
        int teamId, string carNumber, int raceId,
        CarFuelState state,
        DetectedAnchor anchor,
        CancellationToken ct)
    {
        var twoMinAgo = anchor.AtUtc - AnchorAgreementWindow;
        var recent = await db.RefuelEvents
            .Where(r => r.TeamId == teamId && r.CarNumber == carNumber && r.RaceId == raceId
                        && r.DetectedAt >= twoMinAgo && r.DetectedAt <= anchor.AtUtc
                        && r.Source == RefuelSource.AutoDetected)
            .OrderByDescending(r => r.DetectedAt)
            .FirstOrDefaultAsync(ct);

        if (recent is not null)
        {
            var newFlags = recent.AnchorFlags | (int)anchor.Anchor;
            if (newFlags == recent.AnchorFlags) return state; // idempotent — anchor already recorded
            recent.AnchorFlags = newFlags;
            recent.ConfidenceTier = ComputeTier(newFlags, anchor.InPitOrSlowAtAssertion);
            await db.SaveChangesAsync(ct);

            // FuelFull arriving as a corroborating anchor still informs the ECU reset window.
            if (anchor.Anchor == RefuelAnchor.FuelFull && state.CurrentWindowFuelFullAssertedAt is null)
                state.CurrentWindowFuelFullAssertedAt = anchor.AtUtc;

            logger.LogInformation(
                "Merged {Anchor} anchor into RefuelEvent {EventId} for team {TeamId} car {CarNumber} — flags now {Flags}, tier {Tier}",
                anchor.Anchor, recent.Id, teamId, carNumber, (RefuelAnchor)newFlags, recent.ConfidenceTier);
            return state;
        }

        // Brand-new Refuel Event — close the open window (if any), insert event, open new window
        var ev = new RefuelEvent
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            DetectedAt = anchor.AtUtc,
            EnteredFuelGallons = null,
            ConfidenceTier = ComputeTier((int)anchor.Anchor, anchor.InPitOrSlowAtAssertion),
            AnchorFlags = (int)anchor.Anchor,
            Source = RefuelSource.AutoDetected,
            EcuResetState = EcuResetState.Unknown,
            Status = "Pending",
        };
        db.RefuelEvents.Add(ev);
        await db.SaveChangesAsync(ct);

        if (state.OpenFuelWindowId is int openWindowId)
        {
            var oldWindow = await db.FuelWindows.FirstOrDefaultAsync(w => w.Id == openWindowId, ct);
            if (oldWindow is not null && oldWindow.ClosedAt is null)
            {
                oldWindow.ClosedAt = anchor.AtUtc;
                oldWindow.EndRefuelEventId = ev.Id;
                if (oldWindow.OpenedAt < anchor.AtUtc)
                {
                    var elapsedMinutes = (anchor.AtUtc - oldWindow.OpenedAt).TotalMinutes;
                    oldWindow.ObservedDurationSeconds = elapsedMinutes * 60;
                    oldWindow.ObservedConsumptionGalPerMin = ComputeObservedConsumption(state, elapsedMinutes);
                }
                await db.SaveChangesAsync(ct);

                // Learn calibration factor from this just-closed window (no-op when the
                // window's ECU state isn't Reset-Confirmed, the manual override is active,
                // or numbers don't make sense).
                await calibrationLearner.LearnAsync(db, teamId, carNumber, raceId, state, anchor.AtUtc, ct);
            }
        }

        var newWindow = new FuelWindow
        {
            TeamId = teamId,
            CarNumber = carNumber,
            RaceId = raceId,
            StartRefuelEventId = ev.Id,
            OpenedAt = anchor.AtUtc,
        };
        db.FuelWindows.Add(newWindow);
        await db.SaveChangesAsync(ct);

        state.OpenFuelWindowId = newWindow.Id;
        state.OpenFuelWindowStartRefuelEventId = ev.Id;
        state.OpenFuelWindowOpenedAt = anchor.AtUtc;
        state.CurrentWindowFuelFullAssertedAt = anchor.Anchor == RefuelAnchor.FuelFull ? anchor.AtUtc : null;
        state.CurrentWindowTripFuelDroppedAt = null;
        state.CurrentWindowResetClassifyDeadline = anchor.AtUtc + EcuResetClassifyDeadline;
        state.CurrentWindowResetClassified = false;
        state.CurrentWindowEcuResetState = EcuResetState.Unknown;
        state.CurrentWindowEnteredFuelGallons = null;
        state.CurrentWindowFlowMeterFuelUsedAtOpen = state.LastFuelUsedValue;
        state.CurrentWindowThrottleProxyFuelUsedAtOpen = state.LastThrottleProxyFuelUsedValue;
        state.CurrentWindowCloudIntegratedFuelUsedAtOpen = state.CloudIntegratedFuelUsedGallons;
        state.ForceNextSnapshot = true;

        logger.LogInformation(
            "Opened FuelWindow {WindowId} (RefuelEvent {EventId}, anchor {Anchor}) for team {TeamId} car {CarNumber}",
            newWindow.Id, ev.Id, anchor.Anchor, teamId, carNumber);
        return state;
    }

    private static RefuelConfidenceTier ComputeTier(int flags, bool inPitOrSlow)
    {
        var anchorCount = BitOperations.PopCount((uint)flags);
        if (anchorCount >= 2) return RefuelConfidenceTier.High;
        if (anchorCount == 1 && inPitOrSlow) return RefuelConfidenceTier.Medium;
        return RefuelConfidenceTier.Low;
    }

    // Prefer ECU when reset-confirmed (truth); fall back to FlowMeter delta otherwise.
    private static double? ComputeObservedConsumption(CarFuelState state, double elapsedMinutes)
    {
        if (elapsedMinutes <= 0) return null;

        if (state.CurrentWindowEcuResetState == EcuResetState.ResetConfirmed
            && state.LastTripFuelValue is double tripFuel && tripFuel > 0)
        {
            return tripFuel / elapsedMinutes;
        }

        if (state.LastFuelUsedValue is double current
            && state.CurrentWindowFlowMeterFuelUsedAtOpen is double baseline)
        {
            var used = Math.Max(0, current - baseline);
            if (used > 0) return used / elapsedMinutes;
        }

        return null;
    }
}
