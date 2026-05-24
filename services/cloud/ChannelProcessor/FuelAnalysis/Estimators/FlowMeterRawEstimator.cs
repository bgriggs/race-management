namespace ChannelProcessor.FuelAnalysis.Estimators;

/// <summary>
/// FlowMeter Estimator (raw sub-output) — derives fuel remaining from the integrated
/// <c>FuelUsed</c> reserved channel, uncorrected. The <c>flowmeter.corrected</c>
/// counterpart (using a learned calibration factor) is a follow-up slice once the
/// <see cref="Cloud.Shared.Database.Models.FuelAnalysis.CalibrationFactor"/> learner ships.
/// </summary>
public sealed class FlowMeterRawEstimator : IFuelEstimator
{
    public string Name => "flowmeter.raw";
    public bool BypassesRateModel => false;

    private static readonly TimeSpan StaleWatchdog = TimeSpan.FromSeconds(10);
    // Raw FlowMeter sigma is wide — calibration drift + transient air-bubble spikes mean
    // the uncorrected reading routinely disagrees with ECU/PitFill until calibrated.
    private const double SigmaGallons = 2.0;
    private static readonly TimeSpan MinWindowForRate = TimeSpan.FromSeconds(60);

    public EstimatorReading Compute(in EstimatorContext context)
    {
        var state = context.State;
        if (state.LastFuelUsedValue is not double fuelUsed || state.LastFuelUsedTimestamp is not DateTime ts)
            return Unavailable("no FuelUsed reading");

        if ((context.NowUtc - ts) > StaleWatchdog)
            return Unavailable("FuelUsed stale (>10s)");

        var tankCap = context.FuelConfig.TankCapacityGallons;
        if (tankCap <= 0)
            return Unavailable("TankCapacityGallons not configured");

        // Per-window arithmetic: fuel used IN THIS WINDOW = currentFuelUsed − baseline.
        // FuelUsed is monotonic across resets so without the baseline we'd carry over prior windows.
        var baseline = state.CurrentWindowFlowMeterFuelUsedAtOpen ?? fuelUsed;
        var fuelUsedThisWindow = Math.Max(0, fuelUsed - baseline);
        var fuelRemaining = Math.Max(0, tankCap - fuelUsedThisWindow);

        double? baseRate = null;
        if (state.OpenFuelWindowOpenedAt is DateTime openedAt)
        {
            var elapsed = context.NowUtc - openedAt;
            if (elapsed >= MinWindowForRate && fuelUsedThisWindow > 0)
                baseRate = fuelUsedThisWindow / elapsed.TotalMinutes;
        }
        baseRate ??= context.FuelConfig.DefaultConsumptionGalPerMin;

        return new EstimatorReading(true, null, fuelRemaining, SigmaGallons, baseRate);
    }

    private static EstimatorReading Unavailable(string reason) =>
        new(false, reason, null, null, null);
}
