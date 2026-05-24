using ChannelProcessor.FuelAnalysis.State;

namespace ChannelProcessor.FuelAnalysis.Reconciler;

/// <summary>
/// Median-anchored outlier classification with a 30-second debounce per design.md §856–858.
/// An estimator is flagged as an outlier when its range value diverges from the median of
/// available estimators by more than 1.5 × its own sigma; the flag flip only commits after
/// the disagreement persists for 30 s, preventing a single noisy sample from creating UI flap.
/// </summary>
public sealed class OutlierDebouncer
{
    public static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(30);
    public const double MedianDeviationFactor = 1.5;

    /// <summary>
    /// Updates each estimator's debounce entry in <paramref name="state"/> and returns the
    /// committed outlier flag per estimator name. Estimators absent from <paramref name="readings"/>
    /// or unavailable are dropped from the returned map and their debounce entry is reset.
    /// </summary>
    public Dictionary<string, bool> ApplyAndCommit(
        IReadOnlyList<NamedReading> readings,
        CarFuelState state,
        DateTime nowUtc)
    {
        var available = new List<NamedReading>();
        foreach (var r in readings)
        {
            if (r.Reading.Available && r.Reading.RangeGallons is not null && r.Reading.SigmaGallons is not null)
                available.Add(r);
        }

        var result = new Dictionary<string, bool>(available.Count);

        if (available.Count < 2)
        {
            // Outlier detection needs at least two values to anchor against; with one, trust it.
            foreach (var r in available)
            {
                state.OutlierDebounce[r.Name] = new OutlierDebounceEntry { IsOutlier = false, PendingFlipSince = null };
                result[r.Name] = false;
            }
        }
        else
        {
            var median = Median(available);

            foreach (var r in available)
            {
                var value = r.Reading.RangeGallons!.Value;
                var sigma = r.Reading.SigmaGallons!.Value;
                var rawOutlier = Math.Abs(value - median) > MedianDeviationFactor * sigma;

                if (!state.OutlierDebounce.TryGetValue(r.Name, out var entry))
                {
                    entry = new OutlierDebounceEntry();
                    state.OutlierDebounce[r.Name] = entry;
                }

                if (rawOutlier == entry.IsOutlier)
                {
                    // Agrees with committed state — clear any pending flip
                    entry.PendingFlipSince = null;
                }
                else if (entry.PendingFlipSince is null)
                {
                    entry.PendingFlipSince = nowUtc;
                }
                else if ((nowUtc - entry.PendingFlipSince.Value) >= DebounceWindow)
                {
                    entry.IsOutlier = rawOutlier;
                    entry.PendingFlipSince = null;
                }

                result[r.Name] = entry.IsOutlier;
            }
        }

        // Drop debounce entries for estimators not in this tick's results. Runs on every
        // tick (including the <2 branch) so dropped estimators' stale PendingFlipSince
        // can't snap-commit a flip when they later come back — the next observation has
        // to start a fresh debounce window.
        var toRemove = state.OutlierDebounce.Keys.Where(k => !result.ContainsKey(k)).ToList();
        foreach (var k in toRemove) state.OutlierDebounce.Remove(k);

        return result;
    }

    private static double Median(List<NamedReading> readings)
    {
        var values = readings.Select(r => r.Reading.RangeGallons!.Value).ToArray();
        Array.Sort(values);
        var mid = values.Length / 2;
        return values.Length % 2 == 0 ? (values[mid - 1] + values[mid]) / 2.0 : values[mid];
    }
}

public readonly record struct NamedReading(string Name, Estimators.EstimatorReading Reading);
