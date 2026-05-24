namespace ChannelProcessor.FuelAnalysis.Estimators;

/// <summary>
/// <c>throttle.integral</c> sub-output of the ThrottleProxy estimator (design.md §665).
/// Uses the in-car running total <c>ThrottleProxyFuelUsed</c> = <c>k × ∫TPS dt</c>
/// directly, baseline-subtracted at the current FuelWindow's open. Bypasses the rate
/// model — its rate is <c>ThrottleProxyRate</c>, which already encodes the driver's
/// throttle behavior and therefore the pace + flag effects.
/// </summary>
public sealed class ThrottleProxyIntegralEstimator : IFuelEstimator
{
    public string Name => "throttle.integral";
    public bool BypassesRateModel => true;

    private static readonly TimeSpan StaleWatchdog = TimeSpan.FromSeconds(10);
    // Wide sigma — TPS-alone is coarse, indifferent to RPM and gear (design.md §665).
    private const double SigmaGallons = 2.0;
    // Minimum confidence the in-car scalar k must report before we trust the integral at all.
    private const double MinConfidencePercent = 1.0;

    public EstimatorReading Compute(in EstimatorContext context)
    {
        var s = context.State;

        if (s.LastThrottleProxyFuelUsedValue is not double fuelUsed
            || s.LastThrottleProxyFuelUsedTimestamp is not DateTime fuTs)
            return Unavailable("no ThrottleProxyFuelUsed");

        if ((context.NowUtc - fuTs) > StaleWatchdog)
            return Unavailable("ThrottleProxyFuelUsed stale (>10s)");

        if (s.LastThrottleProxyConfidenceValue is not double conf || conf < MinConfidencePercent)
            return Unavailable("ThrottleProxy calibration not converged");

        if (s.LastThrottleProxyRateValue is not double rate || rate <= 0)
            return Unavailable("no ThrottleProxyRate");

        var tankCap = context.FuelConfig.TankCapacityGallons;
        if (tankCap <= 0) return Unavailable("TankCapacityGallons not configured");

        // Baseline-subtracted per-window arithmetic. After rehydrate the baseline is the
        // current value (best-effort), so range reads full-tank until the next window opens.
        var baseline = s.CurrentWindowThrottleProxyFuelUsedAtOpen ?? fuelUsed;
        var usedThisWindow = Math.Max(0, fuelUsed - baseline);
        var fuelRemaining = Math.Max(0, tankCap - usedThisWindow);

        return new EstimatorReading(true, null, fuelRemaining, SigmaGallons, rate);
    }

    private static EstimatorReading Unavailable(string reason) => new(false, reason, null, null, null);
}
