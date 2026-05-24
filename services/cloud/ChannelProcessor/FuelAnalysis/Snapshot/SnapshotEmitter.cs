using ChannelProcessor.FuelAnalysis.Calibration;
using ChannelProcessor.FuelAnalysis.Config;
using ChannelProcessor.FuelAnalysis.Reconciler;
using ChannelProcessor.FuelAnalysis.State;
using Cloud.Shared.FuelAnalysis;

namespace ChannelProcessor.FuelAnalysis.Snapshot;

/// <summary>
/// Per-message decision-and-emit for the Fuel Reconciler's <see cref="Cloud.Shared.FuelAnalysis.FuelRangeSnapshot"/>.
/// Drives the design.md §895 emission cadence: 1-minute steady state, immediate emission
/// on the explicit triggers carried via <see cref="CarFuelState.ForceNextSnapshot"/>
/// (set by <see cref="Windows.FuelWindowLifecycle"/> on new Refuel Events and by
/// <see cref="Refuel.EcuResetClassifier"/> on reset verdicts).
/// <para>
/// Out-of-cadence triggers driven by estimator availability or outlier-status flips that
/// occur silently (e.g., TripFuel going stale &gt; 10 s) wait for the next 1-min tick;
/// adding finer-grained triggers is a future refinement.
/// </para>
/// </summary>
public sealed class SnapshotEmitter(
    ICarFuelConfigReader configReader,
    ICalibrationFactorReader calibrationReader,
    FuelReconciler reconciler,
    IFuelSnapshotStore store,
    FuelSnapshotPublisher publisher,
    TimeProvider timeProvider,
    ILogger<SnapshotEmitter> logger)
{
    public static readonly TimeSpan CadenceInterval = TimeSpan.FromMinutes(1);

    public async Task<CarFuelState> MaybeEmitAsync(
        string carKey, int teamId, string carNumber, int raceId,
        CarFuelState state,
        CancellationToken ct)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var cadenceElapsed = state.LastSnapshotEmittedAt is null
            || (nowUtc - state.LastSnapshotEmittedAt.Value) >= CadenceInterval;

        if (!state.ForceNextSnapshot && !cadenceElapsed)
            return state;

        var fuelConfig = await configReader.GetAsync(carKey, ct);
        if (fuelConfig is null)
        {
            logger.LogDebug("No FuelConfig resolvable for {CarKey}; skipping snapshot", carKey);
            return state;
        }

        var calibration = await calibrationReader.GetLatestAsync(teamId, carNumber, ct);
        var snapshot = reconciler.Build(teamId, carNumber, raceId, state, fuelConfig, calibration, nowUtc);

        await store.SetAsync(carKey, snapshot, ct);
        await publisher.PublishAsync(teamId, carNumber, snapshot, ct);

        state.LastSnapshotEmittedAt = nowUtc;
        state.ForceNextSnapshot = false;
        state.LastEstimatorAvailability = snapshot.Estimators.ToDictionary(e => e.Name, e => e.Available);

        logger.LogDebug(
            "Emitted FuelRangeSnapshot for team {TeamId} car {CarNumber} race {RaceId}: primary {Min} min from {Source}, {Outliers} outliers",
            teamId, carNumber, raceId,
            snapshot.Primary.RangeMinutes?.ToString("F1") ?? "—",
            snapshot.Primary.Source,
            snapshot.Reconciler.OutlierCount);

        return state;
    }
}
