using Racecar.FuelAnalysis;

namespace Racecar.Tests.FuelAnalysis;

[TestClass]
public sealed class ThrottleIntegralTotalizerTests
{
    private static FuelAnalysisOptions DefaultOptions() => new()
    {
        EngineOffResetAfter = TimeSpan.FromSeconds(30),
    };

    private static long S(double seconds) => (long)(seconds * TimeSpan.TicksPerSecond);

    [TestMethod]
    public void First_sample_has_no_attribution_and_no_integral()
    {
        var t = new ThrottleIntegralTotalizer(DefaultOptions());
        var attr = t.OnSample(50, 3000, 0);
        Assert.IsFalse(attr.HasInterval);
        Assert.AreEqual(0, t.Integral);
        Assert.AreEqual(1, t.SampleCount);
    }

    [TestMethod]
    public void Second_sample_integrates_previous_tps_over_elapsed_seconds()
    {
        var t = new ThrottleIntegralTotalizer(DefaultOptions());
        _ = t.OnSample(50, 3000, 0);
        var attr = t.OnSample(80, 3000, S(0.5));

        Assert.IsTrue(attr.HasInterval);
        Assert.AreEqual(50, attr.PrevTps);
        Assert.AreEqual(0.5, attr.DeltaSeconds, 1e-9);
        // 50% × 0.5s = 25 %·s
        Assert.AreEqual(25, t.Integral, 1e-9);
    }

    [TestMethod]
    public void Long_interval_is_dropped_to_absorb_pipeline_hiccups()
    {
        var t = new ThrottleIntegralTotalizer(DefaultOptions());
        _ = t.OnSample(60, 3000, 0);
        var attr = t.OnSample(60, 3000, S(15)); // > 10s skip

        Assert.IsFalse(attr.HasInterval);
        Assert.AreEqual(0, t.Integral);
    }

    [TestMethod]
    public void Engine_off_for_full_window_resets_integral()
    {
        var opts = DefaultOptions();
        opts.EngineOffResetAfter = TimeSpan.FromSeconds(2);
        var t = new ThrottleIntegralTotalizer(opts);

        _ = t.OnSample(80, 3000, S(0));
        _ = t.OnSample(80, 3000, S(0.5)); // build some integral
        Assert.IsGreaterThan(0, t.Integral);

        _ = t.OnSample(0, 0, S(1.0));
        _ = t.OnSample(0, 0, S(1.5));
        _ = t.OnSample(0, 0, S(3.5)); // 2.5s after engine stopped, > 2s threshold

        Assert.AreEqual(0, t.Integral);
    }

    [TestMethod]
    public void Engine_off_below_threshold_does_not_reset()
    {
        var opts = DefaultOptions();
        opts.EngineOffResetAfter = TimeSpan.FromSeconds(30);
        var t = new ThrottleIntegralTotalizer(opts);

        _ = t.OnSample(80, 3000, S(0));
        _ = t.OnSample(80, 3000, S(0.5));
        var before = t.Integral;

        _ = t.OnSample(0, 0, S(1.0));
        _ = t.OnSample(0, 0, S(5.0)); // 4s of engine-off, below threshold

        Assert.IsGreaterThan(0, t.Integral);
        // Some additional integral from the 0.5s @ 80% interval, then 4s @ 0%
        Assert.IsGreaterThanOrEqualTo(before, t.Integral);
    }

    [TestMethod]
    public void Engine_restart_clears_stopped_timer()
    {
        var opts = DefaultOptions();
        opts.EngineOffResetAfter = TimeSpan.FromSeconds(2);
        var t = new ThrottleIntegralTotalizer(opts);

        _ = t.OnSample(80, 3000, S(0));
        _ = t.OnSample(0, 0, S(0.5));
        _ = t.OnSample(0, 0, S(1.5));    // 1s of engine-off
        _ = t.OnSample(50, 3000, S(2.0)); // restart — clears timer
        _ = t.OnSample(50, 3000, S(2.5));

        Assert.IsGreaterThan(0, t.Integral);
    }
}
