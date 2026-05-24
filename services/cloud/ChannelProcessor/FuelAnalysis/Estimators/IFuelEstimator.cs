using ChannelProcessor.FuelAnalysis.State;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Common.FuelAnalysis;

namespace ChannelProcessor.FuelAnalysis.Estimators;

/// <summary>
/// Concurrent fuel-range estimator per design.md §622: each declares its own
/// availability and confidence interval at every reconciler tick; the reconciler
/// cross-validates them. Implementations are stateless — all state lives on
/// <see cref="CarFuelState"/> and on the cached current FuelWindow context.
/// </summary>
public interface IFuelEstimator
{
    /// <summary>Stable name (e.g., "ecu", "flowmeter.raw", "pitfill"). Matches the design's estimator identifiers.</summary>
    string Name { get; }

    /// <summary>
    /// When true, the reconciler uses <see cref="EstimatorReading.BaseRateGalPerMin"/>
    /// directly as the divisor rather than applying the rate-model multipliers. Reserved
    /// for ThrottleProxy whose <c>ThrottleProxyRate</c> already encodes throttle behavior
    /// (and therefore pace + flag effects).
    /// </summary>
    bool BypassesRateModel { get; }

    EstimatorReading Compute(in EstimatorContext context);
}

public readonly record struct EstimatorContext(
    CarFuelState State,
    CarFuelConfig FuelConfig,
    DateTime NowUtc,
    CalibrationFactor? FlowMeterCalibration);

/// <summary>
/// One estimator's output for a single reconciler tick. When <see cref="Available"/> is
/// false, the numeric fields are ignored; <see cref="UnavailableReason"/> flows into the
/// snapshot for engineer visibility.
/// </summary>
public readonly record struct EstimatorReading(
    bool Available,
    string? UnavailableReason,
    double? RangeGallons,
    double? SigmaGallons,
    double? BaseRateGalPerMin);
