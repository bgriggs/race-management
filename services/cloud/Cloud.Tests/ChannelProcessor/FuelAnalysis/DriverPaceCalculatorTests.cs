using ChannelProcessor.FuelAnalysis.Pace;
using ChannelProcessor.FuelAnalysis.State;

namespace Cloud.Tests.ChannelProcessor.FuelAnalysis;

[TestClass]
public class DriverPaceCalculatorTests
{
    private DriverPaceCalculator _calc = null!;

    [TestInitialize]
    public void Setup() => _calc = new DriverPaceCalculator();

    [TestMethod]
    public void Compute_NoRecentLaps_ReturnsNeutral()
    {
        var state = new CarFuelState();

        var result = _calc.Compute(state);

        Assert.AreEqual(PaceResult.Neutral, result);
        Assert.AreEqual(1.0, result.PaceMultiplier);
        Assert.IsNull(result.RecentAvgLapTimeSeconds);
    }

    [TestMethod]
    public void Compute_RecentAvgNonPositive_ReturnsNeutral()
    {
        var state = new CarFuelState { RecentLapTimes = { 0 } };

        var result = _calc.Compute(state);

        Assert.AreEqual(PaceResult.Neutral, result);
    }

    [TestMethod]
    public void Compute_BootstrapFastest_ClampedAtUpperBootstrapBound()
    {
        // Baseline = 100 (firstFullLapTime). Recent avg = 80 → raw 1.25, clamped to 1.05.
        var state = new CarFuelState
        {
            SessionLapTimes = { 100, 99, 101 }, // < 10 → bootstrap regime
            RecentLapTimes = { 80, 80, 80 },
        };

        var result = _calc.Compute(state);

        Assert.AreEqual(DriverPaceCalculator.BootstrapMax, result.PaceMultiplier);
        Assert.AreEqual(80, result.RecentAvgLapTimeSeconds);
    }

    [TestMethod]
    public void Compute_BootstrapSlowest_ClampedAtLowerBootstrapBound()
    {
        var state = new CarFuelState
        {
            SessionLapTimes = { 100 },
            RecentLapTimes = { 200 }, // raw 0.5 → clamped to 0.95
        };

        var result = _calc.Compute(state);

        Assert.AreEqual(DriverPaceCalculator.BootstrapMin, result.PaceMultiplier);
    }

    [TestMethod]
    public void Compute_BootstrapWithinBand_PassesThrough()
    {
        // Baseline 100, recent 102 → raw ≈ 0.98 → within [0.95, 1.05] band.
        var state = new CarFuelState
        {
            SessionLapTimes = { 100 },
            RecentLapTimes = { 102 },
        };

        var result = _calc.Compute(state);

        Assert.AreEqual(100.0 / 102.0, result.PaceMultiplier, 1e-9);
    }

    [TestMethod]
    public void Compute_SteadyState_UsesSessionMedianAsBaseline()
    {
        // 11 laps: median (sorted) of {90, 95, 100, 100, 100, 100, 100, 100, 105, 110, 115} = 100.
        // Recent avg = 100 → pace = 1.0.
        var state = new CarFuelState
        {
            SessionLapTimes = { 100, 95, 110, 100, 90, 105, 100, 100, 115, 100, 100 },
            RecentLapTimes = { 100, 100, 100 },
        };

        var result = _calc.Compute(state);

        Assert.AreEqual(1.0, result.PaceMultiplier, 1e-9);
    }

    [TestMethod]
    public void Compute_SteadyState_EvenCount_AveragesTwoMiddleValues()
    {
        // 10 session laps (exactly the bootstrap threshold → steady regime applies).
        // Sorted: 90,90,100,100,100,100,100,100,110,110 → median = (100+100)/2 = 100.
        var state = new CarFuelState
        {
            SessionLapTimes = { 90, 90, 100, 100, 100, 100, 100, 100, 110, 110 },
            RecentLapTimes = { 100 },
        };

        var result = _calc.Compute(state);

        Assert.AreEqual(1.0, result.PaceMultiplier, 1e-9);
    }

    [TestMethod]
    public void Compute_SteadyState_OutsideBand_ClampedToSteadyMaxOrMin()
    {
        var fast = new CarFuelState
        {
            SessionLapTimes = Enumerable.Repeat(100.0, 10).ToList(),
            RecentLapTimes = { 60 }, // raw ≈ 1.67 → clamped to 1.15
        };
        var slow = new CarFuelState
        {
            SessionLapTimes = Enumerable.Repeat(100.0, 10).ToList(),
            RecentLapTimes = { 200 }, // raw 0.5 → clamped to 0.85
        };

        Assert.AreEqual(DriverPaceCalculator.SteadyMax, _calc.Compute(fast).PaceMultiplier);
        Assert.AreEqual(DriverPaceCalculator.SteadyMin, _calc.Compute(slow).PaceMultiplier);
    }

    [TestMethod]
    public void Compute_ReturnsRecentAverage_NotPerLap()
    {
        var state = new CarFuelState
        {
            SessionLapTimes = { 100 },
            RecentLapTimes = { 100, 102, 98 }, // avg 100
        };

        var result = _calc.Compute(state);

        Assert.AreEqual(100, result.RecentAvgLapTimeSeconds);
    }
}
