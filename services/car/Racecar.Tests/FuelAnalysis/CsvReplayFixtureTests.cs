using System.Globalization;
using Racecar.FuelAnalysis;

namespace Racecar.Tests.FuelAnalysis;

/// <summary>
/// Replays a real recorded AiM-logger session through the throttle-proxy math
/// to verify behavior against a known ground-truth stint: 30 racing laps
/// between pit stops at lap 25 (pit-out) and lap 55 (pit-in), during which the
/// engineer measured 18 gallons consumed. The flow meter in this session was
/// known to be inaccurate — this fixture quantifies the error and verifies
/// the throttle proxy can reach a sane calibration scalar against truth.
/// </summary>
[TestClass]
public sealed class CsvReplayFixtureTests
{
    // Pit-1 ends at beacon[25] = 3888.36 s; pit-2 begins after beacon[55] = 6953.56 s.
    private const double StintStartSeconds = 3888.36;
    private const double StintEndSeconds = 6953.56;
    private const double TruthGallons = 18.0;

    // RpmMax used to size the alpha-N grid. Max RPM in this fixture is ~6.7k;
    // 8000 matches the default in CarConfiguration.FuelConfig.ThrottleConsumption.
    private const int CarRpmMax = 8000;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Stint_replay_produces_plausible_throttle_proxy_calibration_against_known_truth()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "FuelAnalysis", "24.csv");
        Assert.IsTrue(File.Exists(csvPath), $"Fixture CSV not found at {csvPath}");

        var samples = LoadStintSamples(csvPath, StintStartSeconds, StintEndSeconds);
        Assert.IsGreaterThan(0, samples.Count, "No samples loaded for the stint window.");

        // Drive the proxy math directly — same surface ThrottleProxyConsumer
        // exercises internally on every (TPS, RPM) sample.
        var totalizer = new ThrottleIntegralTotalizer(new FuelAnalysisOptions());
        var grid = new AlphaNGrid(CarRpmMax);
        grid.ResetWindowObservations();

        foreach (var s in samples)
        {
            var attribution = totalizer.OnSample(s.Tps, s.Rpm, s.MonotonicTicks);
            if (attribution.HasInterval)
            {
                grid.RecordWindowSample(attribution.PrevTps, attribution.PrevRpm, attribution.DeltaSeconds);
            }
        }

        var stintIntegral = totalizer.Integral;
        var flowMeterDeltaGal = samples[^1].FuelUsed - samples[0].FuelUsed;

        Assert.IsGreaterThan(0.0, stintIntegral, "Throttle integral over the stint must be positive.");
        Assert.IsTrue(double.IsFinite(stintIntegral));

        // Close the calibration window against truth — this is what
        // ThrottleProxyConsumer.ApplyObservation would do given an ECU
        // Reset-Confirmed window. Truth here is the engineer's measured 18 gal.
        var anyCellsUpdated = grid.ApplyWindowClose(TruthGallons, stintIntegral, emaAlpha: 0.3);
        Assert.IsTrue(anyCellsUpdated, "AlphaNGrid.ApplyWindowClose updated no cells.");

        var kTruth = TruthGallons / stintIntegral;
        Assert.IsGreaterThan(1e-5, kTruth, $"k_truth={kTruth:G3} below sane lower bound — possible unit error.");
        Assert.IsLessThan(1e-3, kTruth, $"k_truth={kTruth:G3} above sane upper bound — possible unit error.");

        var cellsVisited = 0;
        for (var i = 0; i < AlphaNGrid.CellCount; i++)
        {
            if (grid.GetCellSampleCount(i) > 0) cellsVisited++;
        }
        Assert.IsGreaterThan(30, cellsVisited,
            $"Grid coverage suspiciously low for a 51-min stint: {cellsVisited}/100 cells visited.");

        // Loop-close sanity: applying k_truth to the integral must reconstruct truth.
        Assert.AreEqual(TruthGallons, kTruth * stintIntegral, delta: 1e-9);

        TestContext.WriteLine("=== Stint replay (pit-out at lap 25 → pit-in at lap 55) ===");
        TestContext.WriteLine($"Samples replayed              : {samples.Count}");
        TestContext.WriteLine($"Stint duration                : {samples[^1].TimeSeconds - samples[0].TimeSeconds,8:F2} s");
        TestContext.WriteLine($"Truth fuel used (engineer)    : {TruthGallons,8:F2} gal");
        TestContext.WriteLine($"Throttle integral over stint  : {stintIntegral,8:F1} %·s");
        TestContext.WriteLine($"Implied k_truth               : {kTruth:G6} gal/(%·s)");
        TestContext.WriteLine($"Flow meter delta over stint   : {flowMeterDeltaGal,8:F3} gal");
        TestContext.WriteLine(
            $"Flow meter reads {100.0 * flowMeterDeltaGal / TruthGallons,5:F1}% of truth — "
            + $"implied calibration factor {TruthGallons / flowMeterDeltaGal:F2}");
        TestContext.WriteLine($"Cells visited by grid         : {cellsVisited}/{AlphaNGrid.CellCount}");
    }

    /// <summary>
    /// Sub-stint cross-check: with both methods constrained to match at the
    /// endpoints (k_truth and factor=4.38 chosen so each integrates to 18 gal
    /// over the stint), how much do their cumulative-consumption curves
    /// diverge in the middle? Tight tracking = throttle proxy adds no
    /// independent signal beyond a scaled flow meter. Loose tracking =
    /// the methods see different physics and can cross-check each other.
    /// </summary>
    [TestMethod]
    public void Throttle_proxy_cumulative_curve_vs_corrected_flow_meter_curve()
    {
        var csvPath = Path.Combine(AppContext.BaseDirectory, "FuelAnalysis", "24.csv");
        var samples = LoadStintSamples(csvPath, StintStartSeconds, StintEndSeconds);
        Assert.IsGreaterThan(0, samples.Count);

        var totalizer = new ThrottleIntegralTotalizer(new FuelAnalysisOptions());

        // First pass: walk the whole stint to get the final throttle integral
        // (so we can compute k_truth) and the flow meter scale factor.
        foreach (var s in samples)
        {
            totalizer.OnSample(s.Tps, s.Rpm, s.MonotonicTicks);
        }
        var stintIntegral = totalizer.Integral;
        var flowMeterDeltaGal = samples[^1].FuelUsed - samples[0].FuelUsed;
        var kTruth = TruthGallons / stintIntegral;
        var flowFactor = TruthGallons / flowMeterDeltaGal;

        // Second pass: snapshot both cumulative curves at 1-minute intervals.
        var totalizer2 = new ThrottleIntegralTotalizer(new FuelAnalysisOptions());
        var snapshotInterval = TimeSpan.FromSeconds(60);
        var nextSnapshotAt = samples[0].TimeSeconds + snapshotInterval.TotalSeconds;
        var startFuel = samples[0].FuelUsed;

        var snapshots = new List<(double tSec, double throttleGal, double flowCorrectedGal)>();
        foreach (var s in samples)
        {
            totalizer2.OnSample(s.Tps, s.Rpm, s.MonotonicTicks);
            if (s.TimeSeconds >= nextSnapshotAt)
            {
                var throttleGal = kTruth * totalizer2.Integral;
                var flowCorrectedGal = flowFactor * (s.FuelUsed - startFuel);
                snapshots.Add((s.TimeSeconds - samples[0].TimeSeconds, throttleGal, flowCorrectedGal));
                nextSnapshotAt += snapshotInterval.TotalSeconds;
            }
        }

        Assert.IsGreaterThan(10, snapshots.Count, "Need a healthy number of intra-stint snapshots.");

        double maxAbsDiff = 0, sumAbsDiff = 0, sumSqDiff = 0;
        double maxDiffElapsedSec = 0;
        double maxDiffThrottle = 0, maxDiffFlow = 0;
        foreach (var (t, throttleGal, flowGal) in snapshots)
        {
            var diff = throttleGal - flowGal;
            var abs = Math.Abs(diff);
            sumAbsDiff += abs;
            sumSqDiff += diff * diff;
            if (abs > maxAbsDiff)
            {
                maxAbsDiff = abs;
                maxDiffElapsedSec = t;
                maxDiffThrottle = throttleGal;
                maxDiffFlow = flowGal;
            }
        }
        var meanAbsDiff = sumAbsDiff / snapshots.Count;
        var rmsDiff = Math.Sqrt(sumSqDiff / snapshots.Count);

        // Sanity: at the final snapshot (closest to stint end), both must be near 18 gal.
        var (_, finalThrottle, finalFlow) = snapshots[^1];
        Assert.AreEqual(finalThrottle, finalFlow, delta: 0.5,
            "Endpoint divergence — both curves are constrained to integrate to truth.");

        TestContext.WriteLine("=== Sub-stint divergence between throttle proxy and corrected flow meter ===");
        TestContext.WriteLine(
            $"Both curves are forced to {TruthGallons:F2} gal at stint end "
            + $"(k_truth={kTruth:G4}, flow factor={flowFactor:F3}).");
        TestContext.WriteLine($"Snapshots                     : {snapshots.Count} @ 60 s");
        TestContext.WriteLine($"Mean abs divergence           : {meanAbsDiff,7:F3} gal");
        TestContext.WriteLine($"RMS divergence                : {rmsDiff,7:F3} gal");
        TestContext.WriteLine(
            $"Max abs divergence            : {maxAbsDiff,7:F3} gal at t+{maxDiffElapsedSec:F0}s "
            + $"(throttle={maxDiffThrottle:F2} vs flow_corrected={maxDiffFlow:F2})");
        TestContext.WriteLine($"Max divergence as % of truth  : {100.0 * maxAbsDiff / TruthGallons,6:F2} %");

        TestContext.WriteLine("");
        TestContext.WriteLine("  t(min)   throttle(gal)   flow_corr(gal)   diff(gal)");
        for (var i = 0; i < snapshots.Count; i += Math.Max(1, snapshots.Count / 12))
        {
            var (t, tg, fg) = snapshots[i];
            TestContext.WriteLine($"  {t / 60.0,6:F1}   {tg,12:F3}   {fg,14:F3}   {tg - fg,9:F3}");
        }
    }

    private static List<Sample> LoadStintSamples(string path, double startSec, double endSec)
    {
        // CSV column order in this fixture (0-indexed):
        //   0  Time         14 RPM           17 FuelUsed
        //   1  GPS Speed    15 PEDAL POSITION 18 FuelLedsCurrent
        const int timeIdx = 0;
        const int rpmIdx = 14;
        const int tpsIdx = 15;
        const int fuelUsedIdx = 17;
        const int minCols = 19;

        var samples = new List<Sample>(capacity: 70_000);
        using var reader = new StreamReader(path);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var cols = line.Split(',');
            if (cols.Length < minCols) continue;

            // Header rows have a non-numeric first field — skip them.
            if (!TryParseField(cols[timeIdx], out var t)) continue;

            if (t < startSec) continue;
            if (t > endSec) break;

            samples.Add(new Sample(
                TimeSeconds: t,
                MonotonicTicks: (long)(t * TimeSpan.TicksPerSecond),
                Tps: ParseField(cols[tpsIdx]),
                Rpm: ParseField(cols[rpmIdx]),
                FuelUsed: ParseField(cols[fuelUsedIdx])));
        }
        return samples;
    }

    private static double ParseField(string s) =>
        double.Parse(s.AsSpan().Trim('"'), NumberStyles.Float, CultureInfo.InvariantCulture);

    private static bool TryParseField(string s, out double value) =>
        double.TryParse(s.AsSpan().Trim('"'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private readonly record struct Sample(
        double TimeSeconds,
        long MonotonicTicks,
        double Tps,
        double Rpm,
        double FuelUsed);
}
