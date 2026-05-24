using ChannelProcessor.FuelAnalysis.ChannelInput;
using ChannelProcessor.FuelAnalysis.Refuel;
using ChannelProcessor.FuelAnalysis.State;
using Cloud.Shared.Database.Models.FuelAnalysis;

namespace Cloud.Tests.ChannelProcessor.FuelAnalysis;

[TestClass]
public class RefuelEventDetectorTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private RefuelEventDetector _detector = null!;
    private CarFuelState _state = null!;

    [TestInitialize]
    public void Setup()
    {
        _detector = new RefuelEventDetector();
        _state = new CarFuelState
        {
            // Stint already old enough to satisfy the FuelFull 15-min guard by default.
            MostRecentStintStartedAt = T0 - TimeSpan.FromMinutes(20),
            LastFuelFullValue = false,
        };
    }

    // ---------------- FuelFull rising edge ----------------

    [TestMethod]
    public void Detect_FuelFullRisingEdge_WhileInPit_ReturnsFuelFullAnchor()
    {
        var inputs = Inputs(fuelFull: (true, T0), inPit: (true, T0));

        var anchors = _detector.Detect(_state, inputs);

        Assert.HasCount(1, anchors);
        Assert.AreEqual(RefuelAnchor.FuelFull, anchors[0].Anchor);
        Assert.AreEqual(T0, anchors[0].AtUtc);
        Assert.IsTrue(anchors[0].InPitOrSlowAtAssertion);
    }

    [TestMethod]
    public void Detect_FuelFullRisingEdge_WhileSlow_ReturnsFuelFullAnchor()
    {
        // Not in pit, but GPS speed below 10 mph.
        var inputs = Inputs(fuelFull: (true, T0), gpsSpeed: (5.0, T0));

        var anchors = _detector.Detect(_state, inputs);

        Assert.HasCount(1, anchors);
        Assert.AreEqual(RefuelAnchor.FuelFull, anchors[0].Anchor);
    }

    [TestMethod]
    public void Detect_FuelFullRisingEdge_NotInPit_NotSlow_ReturnsNoAnchor()
    {
        var inputs = Inputs(fuelFull: (true, T0), gpsSpeed: (45.0, T0));

        var anchors = _detector.Detect(_state, inputs);

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    public void Detect_FuelFullRisingEdge_StintTooYoung_ReturnsNoAnchor()
    {
        _state.MostRecentStintStartedAt = T0 - TimeSpan.FromMinutes(5);
        var inputs = Inputs(fuelFull: (true, T0), inPit: (true, T0));

        var anchors = _detector.Detect(_state, inputs);

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    public void Detect_FuelFullRisingEdge_NoStintRecorded_ReturnsNoAnchor()
    {
        _state.MostRecentStintStartedAt = null;
        var inputs = Inputs(fuelFull: (true, T0), inPit: (true, T0));

        var anchors = _detector.Detect(_state, inputs);

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    public void Detect_FuelFullFallingEdge_ReturnsNoAnchor()
    {
        _state.LastFuelFullValue = true;
        var inputs = Inputs(fuelFull: (false, T0), inPit: (true, T0));

        var anchors = _detector.Detect(_state, inputs);

        Assert.IsEmpty(anchors);
        // State still updated to the new value.
        Assert.IsFalse(_state.LastFuelFullValue);
    }

    [TestMethod]
    public void Detect_FuelFullSecondAssertion_ReturnsNoAnchor()
    {
        _state.LastFuelFullValue = true;
        _state.LastFuelFullTimestamp = T0 - TimeSpan.FromSeconds(1);
        var inputs = Inputs(fuelFull: (true, T0), inPit: (true, T0));

        var anchors = _detector.Detect(_state, inputs);

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    public void Detect_FuelFullOlderTimestamp_IsIgnored()
    {
        _state.LastFuelFullTimestamp = T0;
        _state.LastFuelFullValue = false;
        var older = T0 - TimeSpan.FromSeconds(5);
        var inputs = Inputs(fuelFull: (true, older), inPit: (true, T0));

        var anchors = _detector.Detect(_state, inputs);

        Assert.IsEmpty(anchors);
        Assert.IsFalse(_state.LastFuelFullValue);
        Assert.AreEqual(T0, _state.LastFuelFullTimestamp);
    }

    [TestMethod]
    public void Detect_FuelFullRisingEdge_UsesCachedInPitWhenNotInMessage()
    {
        _state.LastInPitValue = true;
        var inputs = Inputs(fuelFull: (true, T0));

        var anchors = _detector.Detect(_state, inputs);

        Assert.HasCount(1, anchors);
    }

    // ---------------- FuelLevel sustained-rise anchor ----------------

    [TestMethod]
    public void Detect_FuelLevelFirstSample_StartsTracker_NoAnchor()
    {
        // Stationary, no baseline yet.
        var inputs = Inputs(fuelLevel: (5.0, T0), gpsSpeed: (1.0, T0));

        var anchors = _detector.Detect(_state, inputs);

        Assert.IsEmpty(anchors);
        Assert.AreEqual(T0, _state.FuelLevelRiseStartedAt);
        Assert.AreEqual(5.0, _state.FuelLevelAtRiseStart);
    }

    [TestMethod]
    public void Detect_FuelLevelSustainedRise_ReturnsFuelLevelRiseAnchor()
    {
        // Establish baseline at T0.
        _detector.Detect(_state, Inputs(fuelLevel: (5.0, T0), gpsSpeed: (1.0, T0)));
        // 5s later, level rose by 1.5 gal — past both thresholds.
        var t1 = T0 + TimeSpan.FromSeconds(5);
        var anchors = _detector.Detect(_state, Inputs(fuelLevel: (6.5, t1), gpsSpeed: (1.0, t1)));

        Assert.HasCount(1, anchors);
        Assert.AreEqual(RefuelAnchor.FuelLevelRise, anchors[0].Anchor);
        Assert.AreEqual(t1, anchors[0].AtUtc);
        // Tracker reset after firing.
        Assert.IsNull(_state.FuelLevelRiseStartedAt);
        Assert.IsNull(_state.FuelLevelAtRiseStart);
    }

    [TestMethod]
    public void Detect_FuelLevelRise_NotYetSustained_ReturnsNoAnchor()
    {
        _detector.Detect(_state, Inputs(fuelLevel: (5.0, T0), gpsSpeed: (1.0, T0)));
        // Only 2s later — sustained-for is 5s.
        var t1 = T0 + TimeSpan.FromSeconds(2);
        var anchors = _detector.Detect(_state, Inputs(fuelLevel: (7.0, t1), gpsSpeed: (1.0, t1)));

        Assert.IsEmpty(anchors);
        Assert.IsNotNull(_state.FuelLevelRiseStartedAt);
    }

    [TestMethod]
    public void Detect_FuelLevelRise_DeltaTooSmall_ReturnsNoAnchor()
    {
        _detector.Detect(_state, Inputs(fuelLevel: (5.0, T0), gpsSpeed: (1.0, T0)));
        var t1 = T0 + TimeSpan.FromSeconds(10);
        // 0.4 gal — below 1 gal minimum.
        var anchors = _detector.Detect(_state, Inputs(fuelLevel: (5.4, t1), gpsSpeed: (1.0, t1)));

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    public void Detect_FuelLevelRise_NotStationary_ResetsTracker()
    {
        // Start a baseline.
        _detector.Detect(_state, Inputs(fuelLevel: (5.0, T0), gpsSpeed: (1.0, T0)));
        Assert.IsNotNull(_state.FuelLevelRiseStartedAt);

        // Next sample arrives while moving — tracker is cleared regardless of delta.
        var t1 = T0 + TimeSpan.FromSeconds(5);
        var anchors = _detector.Detect(_state, Inputs(fuelLevel: (7.0, t1), gpsSpeed: (50.0, t1)));

        Assert.IsEmpty(anchors);
        Assert.IsNull(_state.FuelLevelRiseStartedAt);
        Assert.IsNull(_state.FuelLevelAtRiseStart);
    }

    [TestMethod]
    public void Detect_FuelLevelGoingDown_ResetsBaseline()
    {
        _detector.Detect(_state, Inputs(fuelLevel: (5.0, T0), gpsSpeed: (1.0, T0)));
        var t1 = T0 + TimeSpan.FromSeconds(3);
        var anchors = _detector.Detect(_state, Inputs(fuelLevel: (4.5, t1), gpsSpeed: (1.0, t1)));

        Assert.IsEmpty(anchors);
        // Baseline restarted at the lower reading.
        Assert.AreEqual(t1, _state.FuelLevelRiseStartedAt);
        Assert.AreEqual(4.5, _state.FuelLevelAtRiseStart);
    }

    [TestMethod]
    public void Detect_FuelLevelOlderTimestamp_IsIgnored()
    {
        _state.LastFuelLevelValue = 5.0;
        _state.LastFuelLevelTimestamp = T0;
        var older = T0 - TimeSpan.FromSeconds(2);

        var anchors = _detector.Detect(_state, Inputs(fuelLevel: (10.0, older), gpsSpeed: (1.0, T0)));

        Assert.IsEmpty(anchors);
        Assert.AreEqual(5.0, _state.LastFuelLevelValue);
        Assert.AreEqual(T0, _state.LastFuelLevelTimestamp);
    }

    // ---------------- Scalar state mirroring ----------------

    [TestMethod]
    public void Detect_GpsAndTripAndFuelUsed_AreMirroredOntoState()
    {
        var inputs = Inputs(
            gpsSpeed: (42.0, T0),
            tripFuel: (3.5, T0),
            fuelUsed: (12.7, T0));

        _detector.Detect(_state, inputs);

        Assert.AreEqual(42.0, _state.LastGpsSpeedMph);
        Assert.AreEqual(T0, _state.LastGpsSpeedTimestamp);
        Assert.AreEqual(3.5, _state.LastTripFuelValue);
        Assert.AreEqual(T0, _state.LastTripFuelTimestamp);
        Assert.AreEqual(12.7, _state.LastFuelUsedValue);
        Assert.AreEqual(T0, _state.LastFuelUsedTimestamp);
    }

    [TestMethod]
    public void Detect_OlderScalarTimestamps_AreIgnored()
    {
        _state.LastGpsSpeedMph = 30.0;
        _state.LastGpsSpeedTimestamp = T0;

        var older = T0 - TimeSpan.FromSeconds(5);
        _detector.Detect(_state, Inputs(gpsSpeed: (99.0, older)));

        Assert.AreEqual(30.0, _state.LastGpsSpeedMph);
        Assert.AreEqual(T0, _state.LastGpsSpeedTimestamp);
    }

    // ---------------- Helpers ----------------

    private static FuelInputs Inputs(
        (double Value, DateTime Ts)? fuelLevel = null,
        (double Value, DateTime Ts)? fuelUsed = null,
        (double Value, DateTime Ts)? tripFuel = null,
        (bool   Value, DateTime Ts)? fuelFull = null,
        (bool   Value, DateTime Ts)? inPit    = null,
        (double Value, DateTime Ts)? gpsSpeed = null) =>
        new(
            FuelLevel: fuelLevel is var fl && fl.HasValue ? new TimestampedDouble(fl.Value.Value, fl.Value.Ts) : null,
            FuelUsed:  fuelUsed  is var fu && fu.HasValue ? new TimestampedDouble(fu.Value.Value, fu.Value.Ts) : null,
            TripFuel:  tripFuel  is var tf && tf.HasValue ? new TimestampedDouble(tf.Value.Value, tf.Value.Ts) : null,
            FuelFull:  fuelFull  is var ff && ff.HasValue ? new TimestampedBool(ff.Value.Value, ff.Value.Ts)   : null,
            InPit:     inPit     is var ip && ip.HasValue ? new TimestampedBool(ip.Value.Value, ip.Value.Ts)   : null,
            GpsSpeedMph: gpsSpeed is var gs && gs.HasValue ? new TimestampedDouble(gs.Value.Value, gs.Value.Ts) : null,
            LastLapTimeSeconds: null,
            ManualFuelAddedGallons: null,
            ThrottleProxyFuelUsed: null,
            ThrottleProxyRate: null,
            ThrottleProxyConfidence: null,
            ThrottleProxyGridCoverage: null);
}
