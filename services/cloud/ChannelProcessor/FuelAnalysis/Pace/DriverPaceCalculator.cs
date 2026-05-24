using ChannelProcessor.FuelAnalysis.State;

namespace ChannelProcessor.FuelAnalysis.Pace;

/// <summary>
/// Computes <c>paceMultiplier = sessionBaselineLapTime ÷ recentAvgLapTime</c> per
/// design.md §798. Bootstraps off the first valid lap and clamps to ±5% until 10 valid
/// laps have been observed; thereafter uses session median with ±15% clamp.
/// </summary>
public sealed class DriverPaceCalculator
{
    public const int BootstrapLapCount = 10;
    public const double BootstrapMin = 0.95;
    public const double BootstrapMax = 1.05;
    public const double SteadyMin = 0.85;
    public const double SteadyMax = 1.15;

    public PaceResult Compute(CarFuelState state)
    {
        if (state.RecentLapTimes.Count == 0) return PaceResult.Neutral;

        var recentAvg = state.RecentLapTimes.Average();
        if (recentAvg <= 0) return PaceResult.Neutral;

        double baseline;
        double minClamp, maxClamp;

        if (state.SessionLapTimes.Count < BootstrapLapCount)
        {
            baseline = state.SessionLapTimes[0]; // firstFullLapTime
            minClamp = BootstrapMin;
            maxClamp = BootstrapMax;
        }
        else
        {
            baseline = Median(state.SessionLapTimes);
            minClamp = SteadyMin;
            maxClamp = SteadyMax;
        }

        var raw = baseline / recentAvg;
        var clamped = Math.Clamp(raw, minClamp, maxClamp);
        return new PaceResult(clamped, recentAvg);
    }

    private static double Median(List<double> values)
    {
        var sorted = values.ToArray();
        Array.Sort(sorted);
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }
}

public readonly record struct PaceResult(double PaceMultiplier, double? RecentAvgLapTimeSeconds)
{
    public static readonly PaceResult Neutral = new(1.0, null);
}
