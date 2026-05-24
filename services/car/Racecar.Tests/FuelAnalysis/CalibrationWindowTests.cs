using Racecar.FuelAnalysis;

namespace Racecar.Tests.FuelAnalysis;

[TestClass]
public sealed class CalibrationWindowTests
{
    private static long S(double seconds) => (long)(seconds * TimeSpan.TicksPerSecond);

    [TestMethod]
    public void New_window_starts_closed()
    {
        var w = new CalibrationWindow();
        Assert.IsFalse(w.IsOpen);
    }

    [TestMethod]
    public void Close_on_closed_window_returns_null()
    {
        var w = new CalibrationWindow();
        Assert.IsNull(w.Close(closeTripFuel: 5.0, closeIntegral: 1000, S(60)));
    }

    [TestMethod]
    public void Round_trip_produces_observation_with_deltas()
    {
        var w = new CalibrationWindow();
        w.Open(openTripFuel: 0.0, openIntegral: 0.0, S(0));
        var obs = w.Close(closeTripFuel: 5.0, closeIntegral: 12000, S(60 * 30));

        Assert.IsNotNull(obs);
        Assert.AreEqual(5.0, obs!.Value.DeltaFuelGallons, 1e-9);
        Assert.AreEqual(12000, obs.Value.DeltaIntegral, 1e-9);
        Assert.AreEqual(TimeSpan.FromMinutes(30), obs.Value.Elapsed);
        Assert.IsFalse(w.IsOpen, "Window should close after Close().");
    }

    [TestMethod]
    public void Close_with_zero_or_negative_deltas_returns_null()
    {
        var w = new CalibrationWindow();
        w.Open(openTripFuel: 2.0, openIntegral: 1000, S(0));

        var obs1 = w.Close(closeTripFuel: 2.0, closeIntegral: 2000, S(60));
        Assert.IsNull(obs1, "Zero ΔFuel must invalidate the window.");

        // Re-open to test integral side.
        w.Open(openTripFuel: 2.0, openIntegral: 2000, S(60));
        var obs2 = w.Close(closeTripFuel: 4.0, closeIntegral: 2000, S(120));
        Assert.IsNull(obs2, "Zero ΔIntegral must invalidate the window.");
    }
}
