namespace ChannelProcessor.FuelAnalysis.Estimators;

/// <summary>
/// <c>flowmeter.corrected</c> — design.md §645: <c>fuelRemaining = tankCapacity −
/// (fuelUsed × calibrationFactor)</c>, applied to the FlowMeter's per-window arithmetic
/// (current FuelUsed − baseline at window open). Available only when a
/// <see cref="Cloud.Shared.Database.Models.FuelAnalysis.CalibrationFactor"/> exists from
/// any source (learned, manual override, or reset).
/// <para>
/// Tighter sigma than <see cref="FlowMeterRawEstimator"/> because the calibration removes
/// the systematic offset; some residual noise remains from flow-rate variation and
/// transient air-bubble spikes, so sigma stays above ECU's tight 0.5 gal.
/// </para>
/// </summary>
public sealed class FlowMeterCorrectedEstimator : IFuelEstimator
{
    public string Name => "flowmeter.corrected";
    public bool BypassesRateModel => false;

    private static readonly TimeSpan StaleWatchdog = TimeSpan.FromSeconds(10);
    private const double SigmaGallons = 1.0;
    private static readonly TimeSpan MinWindowForRate = TimeSpan.FromSeconds(60);

    public EstimatorReading Compute(in EstimatorContext context)
    {
        if (context.FlowMeterCalibration is null)
            return Unavailable("no calibration factor recorded yet");

        var state = context.State;
        if (state.LastFuelUsedValue is not double fuelUsed || state.LastFuelUsedTimestamp is not DateTime ts)
            return Unavailable("no FuelUsed reading");
        if ((context.NowUtc - ts) > StaleWatchdog)
            return Unavailable("FuelUsed stale (>10s)");

        var tankCap = context.FuelConfig.TankCapacityGallons;
        if (tankCap <= 0)
            return Unavailable("TankCapacityGallons not configured");

        var factor = context.FlowMeterCalibration.Value;
        if (factor <= 0)
            return Unavailable("calibration factor not positive");

        var baseline = state.CurrentWindowFlowMeterFuelUsedAtOpen ?? fuelUsed;
        var rawUsedThisWindow = Math.Max(0, fuelUsed - baseline);
        var correctedUsed = rawUsedThisWindow * factor;
        var fuelRemaining = Math.Max(0, tankCap - correctedUsed);

        double? baseRate = null;
        if (state.OpenFuelWindowOpenedAt is DateTime openedAt)
        {
            var elapsed = context.NowUtc - openedAt;
            if (elapsed >= MinWindowForRate && correctedUsed > 0)
                baseRate = correctedUsed / elapsed.TotalMinutes;
        }
        baseRate ??= context.FuelConfig.DefaultConsumptionGalPerMin;

        return new EstimatorReading(true, null, fuelRemaining, SigmaGallons, baseRate);
    }

    private static EstimatorReading Unavailable(string reason) =>
        new(false, reason, null, null, null);
}
