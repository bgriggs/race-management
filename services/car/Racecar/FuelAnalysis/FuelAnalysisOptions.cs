namespace Racecar.FuelAnalysis;

/// <summary>
/// Tunables for the in-car throttle proxy module. Bound from the
/// <c>FuelAnalysis</c> configuration section.
/// </summary>
public sealed class FuelAnalysisOptions
{
    /// <summary>
    /// Path to the calibration JSON file. Defaults to
    /// <c>/etc/racecar/fuel-calibration.json</c>; overridable for tests.
    /// </summary>
    public string CalibrationFilePath { get; set; } = "/etc/racecar/fuel-calibration.json";

    /// <summary>
    /// Per-grid-cell observation count (window-closes that touched the cell)
    /// at which a cell is considered fully calibrated for confidence scoring.
    /// </summary>
    public int MinCellSamples { get; set; } = 30;

    /// <summary>
    /// Scalar-k EMA sample count at which the k-convergence score saturates to 1.
    /// </summary>
    public int KConvergenceSampleCount { get; set; } = 10;

    /// <summary>Exponential moving average factor for calibration learning.</summary>
    public double LearningEmaAlpha { get; set; } = 0.3;

    /// <summary>
    /// Continuous duration with <c>EngineRPM = 0</c> after which the throttle
    /// integral is reset to zero.
    /// </summary>
    public TimeSpan EngineOffResetAfter { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Window after a <c>FuelFull</c> assertion within which a <c>TripFuel</c>
    /// drop below <see cref="EcuResetTripFuelThresholdGallons"/> classifies as
    /// Reset-Confirmed.
    /// </summary>
    public TimeSpan EcuResetWindow { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// <c>TripFuel</c> value (gal) below which the ECU is considered to have
    /// reset its trip counter.
    /// </summary>
    public double EcuResetTripFuelThresholdGallons { get; set; } = 1.0;

    /// <summary>Rate-emission cadence (1 Hz steady state).</summary>
    public TimeSpan EmitInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Coverage-emission cadence (low rate, diagnostic).</summary>
    public TimeSpan CoverageEmitInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>EMA time constant for the published <c>ThrottleProxyRate</c>.</summary>
    public TimeSpan RateEmaTimeConstant { get; set; } = TimeSpan.FromSeconds(2);
}
