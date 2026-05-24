using ChannelProcessor.FuelAnalysis.ChannelInput;
using ChannelProcessor.FuelAnalysis.State;

namespace ChannelProcessor.FuelAnalysis.Estimators;

/// <summary>
/// Per-message updater for the four <c>ThrottleProxy*</c> reserved channels (in-car
/// CarToCloud) and the cloud-maintained running integral of <c>ThrottleProxyRate</c>.
/// Both <see cref="ThrottleProxyIntegralEstimator"/> and
/// <see cref="ThrottleProxyGridEstimator"/> read the values this tracker writes to state.
/// <para>
/// The running ∫ <c>ThrottleProxyRate</c> dt is the cloud-side equivalent of the in-car
/// scalar totalizer but driven by the per-cell alpha-N grid rate — design.md §665. We use
/// trapezoidal integration between consecutive samples and skip gaps longer than
/// <see cref="MaxIntegrationGap"/> (likely a telemetry outage rather than a long
/// cell-bound period).
/// </para>
/// </summary>
public sealed class ThrottleProxyTracker
{
    private static readonly TimeSpan MaxIntegrationGap = TimeSpan.FromMinutes(5);

    public CarFuelState Apply(CarFuelState state, FuelInputs inputs)
    {
        if (inputs.ThrottleProxyFuelUsed is TimestampedDouble fuelUsed
            && (state.LastThrottleProxyFuelUsedTimestamp is not DateTime fuTs || fuelUsed.TimestampUtc > fuTs))
        {
            state.LastThrottleProxyFuelUsedValue = fuelUsed.Value;
            state.LastThrottleProxyFuelUsedTimestamp = fuelUsed.TimestampUtc;
        }

        if (inputs.ThrottleProxyConfidence is TimestampedDouble conf
            && (state.LastThrottleProxyConfidenceTimestamp is not DateTime cTs || conf.TimestampUtc > cTs))
        {
            state.LastThrottleProxyConfidenceValue = conf.Value;
            state.LastThrottleProxyConfidenceTimestamp = conf.TimestampUtc;
        }

        if (inputs.ThrottleProxyGridCoverage is TimestampedDouble cov
            && (state.LastThrottleProxyGridCoverageTimestamp is not DateTime covTs || cov.TimestampUtc > covTs))
        {
            state.LastThrottleProxyGridCoverageValue = cov.Value;
            state.LastThrottleProxyGridCoverageTimestamp = cov.TimestampUtc;
        }

        if (inputs.ThrottleProxyRate is TimestampedDouble rate
            && (state.LastThrottleProxyRateTimestamp is not DateTime rTs || rate.TimestampUtc > rTs))
        {
            if (state.LastThrottleProxyRateValue is double prevRate
                && state.LastThrottleProxyRateTimestamp is DateTime prev)
            {
                var gap = rate.TimestampUtc - prev;
                if (gap > TimeSpan.Zero && gap <= MaxIntegrationGap)
                {
                    var avgGalPerMin = (prevRate + rate.Value) / 2.0;
                    state.CloudIntegratedFuelUsedGallons =
                        (state.CloudIntegratedFuelUsedGallons ?? 0) + avgGalPerMin * gap.TotalMinutes;
                }
                else
                {
                    state.CloudIntegratedFuelUsedGallons ??= 0;
                }
            }
            else
            {
                // First-ever sample (or first after rehydrate): seed at 0 but don't integrate.
                state.CloudIntegratedFuelUsedGallons ??= 0;
            }
            state.LastThrottleProxyRateValue = rate.Value;
            state.LastThrottleProxyRateTimestamp = rate.TimestampUtc;
        }

        return state;
    }
}
