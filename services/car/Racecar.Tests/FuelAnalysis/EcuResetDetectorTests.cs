using Racecar.FuelAnalysis;

namespace Racecar.Tests.FuelAnalysis;

[TestClass]
public sealed class EcuResetDetectorTests
{
    private static FuelAnalysisOptions DefaultOptions() => new()
    {
        EcuResetWindow = TimeSpan.FromSeconds(60),
        EcuResetTripFuelThresholdGallons = 1.0,
    };

    private static long S(double seconds) => (long)(seconds * TimeSpan.TicksPerSecond);

    [TestMethod]
    public void Fuel_full_then_trip_fuel_drop_inside_window_confirms_reset()
    {
        var d = new EcuResetDetector(DefaultOptions());

        d.OnTripFuel(8.5, S(0)); // pre-anchor reading
        d.OnFuelFull(1.0, S(10)); // anchor
        Assert.IsFalse(d.ResetJustDetected, "Anchor alone with high TripFuel must not classify yet.");

        d.OnTripFuel(0.2, S(40)); // drop inside the 60s window
        Assert.AreEqual(EcuResetState.ResetConfirmed, d.CurrentState);
        Assert.IsTrue(d.ResetJustDetected);
    }

    [TestMethod]
    public void Trip_fuel_drop_before_fuel_full_then_assertion_inside_window_also_confirms()
    {
        var d = new EcuResetDetector(DefaultOptions());

        d.OnTripFuel(8.5, S(0));
        d.OnTripFuel(0.3, S(10)); // pre-emptive drop
        d.OnFuelFull(1.0, S(20)); // anchor arrives — last TripFuel is below threshold, within window

        Assert.AreEqual(EcuResetState.ResetConfirmed, d.CurrentState);
        Assert.IsTrue(d.ResetJustDetected);
    }

    [TestMethod]
    public void Reset_just_detected_is_a_single_edge()
    {
        var d = new EcuResetDetector(DefaultOptions());
        d.OnFuelFull(1.0, S(0));
        d.OnTripFuel(0.1, S(5));
        Assert.IsTrue(d.ResetJustDetected);

        d.OnTripFuel(0.05, S(10));
        Assert.IsFalse(d.ResetJustDetected);
    }

    [TestMethod]
    public void No_trip_fuel_drop_after_window_classifies_as_unreset()
    {
        var d = new EcuResetDetector(DefaultOptions());

        d.OnTripFuel(8.0, S(0));
        d.OnFuelFull(1.0, S(0));
        d.Tick(S(70)); // past the 60s window without a drop

        Assert.AreEqual(EcuResetState.Unreset, d.CurrentState);
        Assert.IsFalse(d.ResetJustDetected);
    }

    [TestMethod]
    public void Late_trip_fuel_drop_after_window_does_not_confirm()
    {
        var d = new EcuResetDetector(DefaultOptions());

        d.OnTripFuel(8.0, S(0));
        d.OnFuelFull(1.0, S(0));
        d.OnTripFuel(0.1, S(120)); // outside window

        Assert.AreNotEqual(EcuResetState.ResetConfirmed, d.CurrentState);
        Assert.IsFalse(d.ResetJustDetected);
    }
}
