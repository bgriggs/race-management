namespace ChannelProcessor.FuelAnalysis.Estimators;

/// <summary>
/// <c>throttle.grid</c> sub-output of the ThrottleProxy estimator (design.md §666).
/// Uses the cloud-maintained ∫ <c>ThrottleProxyRate</c> dt (driven by the per-cell alpha-N
/// grid lookup) — narrower sigma than <see cref="ThrottleProxyIntegralEstimator"/> when
/// the grid is well-populated, unavailable otherwise.
/// <para>
/// Bypasses the rate model — <c>ThrottleProxyRate</c> already encodes pace + flag.
/// </para>
/// </summary>
public sealed class ThrottleProxyGridEstimator : IFuelEstimator
{
    public string Name => "throttle.grid";
    public bool BypassesRateModel => true;

    private static readonly TimeSpan StaleWatchdog = TimeSpan.FromSeconds(10);
    // Design §666 thresholds: current cell ≥ 30 samples (proxied by ThrottleProxyConfidence
    // being well above its gridCellScore floor) AND grid coverage ≥ 40%.
    private const double MinConfidencePercent = 75.0;
    private const double MinGridCoveragePercent = 40.0;
    // Sigma scales inversely with confidence — 100% → 0.5 gal, 75% → 1.5 gal (linear).
    private const double SigmaAtFullConfidence = 0.5;
    private const double SigmaAtMinConfidence = 1.5;

    public EstimatorReading Compute(in EstimatorContext context)
    {
        var s = context.State;

        if (s.LastThrottleProxyConfidenceValue is not double conf || conf < MinConfidencePercent)
            return Unavailable($"ThrottleProxy confidence below {MinConfidencePercent:F0}% threshold");
        if (s.LastThrottleProxyGridCoverageValue is not double cov || cov < MinGridCoveragePercent)
            return Unavailable($"ThrottleProxy grid coverage below {MinGridCoveragePercent:F0}% threshold");

        if (s.LastThrottleProxyRateTimestamp is not DateTime rateTs)
            return Unavailable("no ThrottleProxyRate");
        if ((context.NowUtc - rateTs) > StaleWatchdog)
            return Unavailable("ThrottleProxyRate stale (>10s)");
        if (s.LastThrottleProxyRateValue is not double rate || rate <= 0)
            return Unavailable("no ThrottleProxyRate");

        if (s.CloudIntegratedFuelUsedGallons is not double integrated)
            return Unavailable("cloud rate-integral not yet initialized");
        if (s.CurrentWindowCloudIntegratedFuelUsedAtOpen is not double baseline)
            return Unavailable("no rate-integral baseline at window open (rehydrate)");

        var tankCap = context.FuelConfig.TankCapacityGallons;
        if (tankCap <= 0) return Unavailable("TankCapacityGallons not configured");

        var usedThisWindow = Math.Max(0, integrated - baseline);
        var fuelRemaining = Math.Max(0, tankCap - usedThisWindow);

        // Linear interpolation between min-confidence and full-confidence sigmas.
        var t = (conf - MinConfidencePercent) / (100.0 - MinConfidencePercent); // 0..1
        var sigma = SigmaAtMinConfidence + (SigmaAtFullConfidence - SigmaAtMinConfidence) * t;

        return new EstimatorReading(true, null, fuelRemaining, sigma, rate);
    }

    private static EstimatorReading Unavailable(string reason) => new(false, reason, null, null, null);
}
