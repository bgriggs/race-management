using ChannelProcessor.FuelAnalysis.Estimators;
using ChannelProcessor.FuelAnalysis.Reconciler;
using ChannelProcessor.FuelAnalysis.State;

namespace Cloud.Tests.ChannelProcessor.FuelAnalysis;

[TestClass]
public class OutlierDebouncerTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private OutlierDebouncer _debouncer = null!;
    private CarFuelState _state = null!;

    [TestInitialize]
    public void Setup()
    {
        _debouncer = new OutlierDebouncer();
        _state = new CarFuelState();
    }

    [TestMethod]
    public void Apply_AllReadingsUnavailable_ReturnsEmpty()
    {
        var readings = new[]
        {
            new NamedReading("ecu", Unavailable()),
            new NamedReading("flowmeter.raw", Unavailable()),
        };

        var result = _debouncer.ApplyAndCommit(readings, _state, T0);

        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void Apply_SingleAvailableReading_TrustedAsNotOutlier()
    {
        var readings = new[]
        {
            new NamedReading("ecu", Available(range: 10, sigma: 1)),
            new NamedReading("flowmeter.raw", Unavailable()),
        };

        var result = _debouncer.ApplyAndCommit(readings, _state, T0);

        Assert.HasCount(1, result);
        Assert.IsFalse(result["ecu"]);
        Assert.IsTrue(_state.OutlierDebounce.ContainsKey("ecu"));
        Assert.IsFalse(_state.OutlierDebounce["ecu"].IsOutlier);
    }

    [TestMethod]
    public void Apply_TwoReadingsAgree_NoOutlier()
    {
        var readings = new[]
        {
            new NamedReading("ecu", Available(range: 10, sigma: 1)),
            new NamedReading("flowmeter.raw", Available(range: 10.5, sigma: 1)),
        };

        var result = _debouncer.ApplyAndCommit(readings, _state, T0);

        Assert.IsFalse(result["ecu"]);
        Assert.IsFalse(result["flowmeter.raw"]);
    }

    [TestMethod]
    public void Apply_RawOutlier_StartsPendingFlip_NotYetCommitted()
    {
        // ecu=10, flowmeter=10, pitfill=30; median=10, pitfill deviates by 20 >> 1.5*1.
        var readings = new[]
        {
            new NamedReading("ecu", Available(10, 1)),
            new NamedReading("flowmeter.raw", Available(10, 1)),
            new NamedReading("pitfill", Available(30, 1)),
        };

        var result = _debouncer.ApplyAndCommit(readings, _state, T0);

        Assert.IsFalse(result["pitfill"]); // raw outlier but not yet committed
        Assert.AreEqual(T0, _state.OutlierDebounce["pitfill"].PendingFlipSince);
        Assert.IsFalse(_state.OutlierDebounce["pitfill"].IsOutlier);
    }

    [TestMethod]
    public void Apply_OutlierPersistsForDebounceWindow_CommitsFlip()
    {
        var readings = new[]
        {
            new NamedReading("ecu", Available(10, 1)),
            new NamedReading("flowmeter.raw", Available(10, 1)),
            new NamedReading("pitfill", Available(30, 1)),
        };

        _debouncer.ApplyAndCommit(readings, _state, T0);
        // Just before window — still not committed.
        var almost = T0 + OutlierDebouncer.DebounceWindow - TimeSpan.FromSeconds(1);
        var midResult = _debouncer.ApplyAndCommit(readings, _state, almost);
        Assert.IsFalse(midResult["pitfill"]);

        // Past window — commits.
        var past = T0 + OutlierDebouncer.DebounceWindow;
        var finalResult = _debouncer.ApplyAndCommit(readings, _state, past);
        Assert.IsTrue(finalResult["pitfill"]);
        Assert.IsTrue(_state.OutlierDebounce["pitfill"].IsOutlier);
        Assert.IsNull(_state.OutlierDebounce["pitfill"].PendingFlipSince);
    }

    [TestMethod]
    public void Apply_RawValueRecovers_PendingFlipClears()
    {
        var outlierReadings = new[]
        {
            new NamedReading("ecu", Available(10, 1)),
            new NamedReading("flowmeter.raw", Available(10, 1)),
            new NamedReading("pitfill", Available(30, 1)),
        };
        _debouncer.ApplyAndCommit(outlierReadings, _state, T0);
        Assert.IsNotNull(_state.OutlierDebounce["pitfill"].PendingFlipSince);

        // Now all three agree again.
        var goodReadings = new[]
        {
            new NamedReading("ecu", Available(10, 1)),
            new NamedReading("flowmeter.raw", Available(10, 1)),
            new NamedReading("pitfill", Available(10, 1)),
        };
        _debouncer.ApplyAndCommit(goodReadings, _state, T0 + TimeSpan.FromSeconds(5));

        Assert.IsNull(_state.OutlierDebounce["pitfill"].PendingFlipSince);
        Assert.IsFalse(_state.OutlierDebounce["pitfill"].IsOutlier);
    }

    [TestMethod]
    public void Apply_LargerSigmaTolerantToDeviation_NoPendingFlip()
    {
        // pitfill deviates by 20 but its sigma is 100 → 1.5*100=150, no outlier.
        var readings = new[]
        {
            new NamedReading("ecu", Available(10, 1)),
            new NamedReading("flowmeter.raw", Available(10, 1)),
            new NamedReading("pitfill", Available(30, 100)),
        };

        var result = _debouncer.ApplyAndCommit(readings, _state, T0);

        Assert.IsFalse(result["pitfill"]);
        Assert.IsNull(_state.OutlierDebounce["pitfill"].PendingFlipSince);
    }

    [TestMethod]
    public void Apply_DroppedEstimator_DebounceEntryRemoved()
    {
        // Three estimators initially so the multi-estimator code path (which owns the
        // stale-entry cleanup) runs on both ticks.
        var first = new[]
        {
            new NamedReading("ecu", Available(10, 1)),
            new NamedReading("flowmeter.raw", Available(10, 1)),
            new NamedReading("pitfill", Available(10, 1)),
        };
        _debouncer.ApplyAndCommit(first, _state, T0);
        Assert.IsTrue(_state.OutlierDebounce.ContainsKey("pitfill"));

        // pitfill now unavailable; the other two keep us on the multi-estimator path.
        var second = new[]
        {
            new NamedReading("ecu", Available(10, 1)),
            new NamedReading("flowmeter.raw", Available(10, 1)),
            new NamedReading("pitfill", Unavailable()),
        };
        _debouncer.ApplyAndCommit(second, _state, T0 + TimeSpan.FromSeconds(1));

        Assert.IsFalse(_state.OutlierDebounce.ContainsKey("pitfill"));
    }

    [TestMethod]
    public void Apply_SingleAvailable_ClearsPendingFlipFromPriorTick()
    {
        // Set up a pending-flip pitfill, then drop the others so pitfill is alone.
        var readings = new[]
        {
            new NamedReading("ecu", Available(10, 1)),
            new NamedReading("flowmeter.raw", Available(10, 1)),
            new NamedReading("pitfill", Available(30, 1)),
        };
        _debouncer.ApplyAndCommit(readings, _state, T0);
        Assert.IsNotNull(_state.OutlierDebounce["pitfill"].PendingFlipSince);

        var alone = new[]
        {
            new NamedReading("ecu", Unavailable()),
            new NamedReading("flowmeter.raw", Unavailable()),
            new NamedReading("pitfill", Available(30, 1)),
        };
        var result = _debouncer.ApplyAndCommit(alone, _state, T0 + TimeSpan.FromSeconds(1));

        Assert.IsFalse(result["pitfill"]);
        Assert.IsNull(_state.OutlierDebounce["pitfill"].PendingFlipSince);
    }

    [TestMethod]
    public void Apply_DropToSingleAvailable_PrunesStaleEntries()
    {
        // Two estimators tracked, then both-minus-one drops out → single available.
        // The dropped estimator's entry must not linger in state.
        var first = new[]
        {
            new NamedReading("ecu", Available(10, 1)),
            new NamedReading("flowmeter.raw", Available(10, 1)),
        };
        _debouncer.ApplyAndCommit(first, _state, T0);
        Assert.IsTrue(_state.OutlierDebounce.ContainsKey("flowmeter.raw"));

        var second = new[]
        {
            new NamedReading("ecu", Available(10, 1)),
            new NamedReading("flowmeter.raw", Unavailable()),
        };
        _debouncer.ApplyAndCommit(second, _state, T0 + TimeSpan.FromSeconds(1));

        Assert.IsFalse(_state.OutlierDebounce.ContainsKey("flowmeter.raw"));
        Assert.IsTrue(_state.OutlierDebounce.ContainsKey("ecu"));
    }

    [TestMethod]
    public void Apply_EstimatorReturnsAfterDropOut_RequiresFreshDebounceWindow()
    {
        // Regression: a stale PendingFlipSince must not survive an availability gap and
        // snap-commit a flip when the estimator comes back >30s later.
        var initial = new[]
        {
            new NamedReading("ecu", Available(10, 1)),
            new NamedReading("flowmeter.raw", Available(10, 1)),
            new NamedReading("pitfill", Available(30, 1)), // raw outlier
        };
        _debouncer.ApplyAndCommit(initial, _state, T0);
        Assert.AreEqual(T0, _state.OutlierDebounce["pitfill"].PendingFlipSince);

        // pitfill drops out and the others go with it — entire state pruned.
        var allOut = new[]
        {
            new NamedReading("ecu", Unavailable()),
            new NamedReading("flowmeter.raw", Unavailable()),
            new NamedReading("pitfill", Unavailable()),
        };
        _debouncer.ApplyAndCommit(allOut, _state, T0 + TimeSpan.FromSeconds(5));
        Assert.IsEmpty(_state.OutlierDebounce);

        // 2 minutes later, all three return. pitfill is still a raw outlier, but its
        // pending flip has to start fresh — NOT inherit the old T0 timestamp.
        var returned = new[]
        {
            new NamedReading("ecu", Available(10, 1)),
            new NamedReading("flowmeter.raw", Available(10, 1)),
            new NamedReading("pitfill", Available(30, 1)),
        };
        var tReturn = T0 + TimeSpan.FromMinutes(2);
        var result = _debouncer.ApplyAndCommit(returned, _state, tReturn);

        Assert.IsFalse(result["pitfill"], "fresh observation must not snap-commit the flip");
        Assert.AreEqual(tReturn, _state.OutlierDebounce["pitfill"].PendingFlipSince);
    }

    // ---------------- Helpers ----------------

    private static EstimatorReading Available(double range, double sigma) =>
        new(true, null, range, sigma, BaseRateGalPerMin: 1.0);

    private static EstimatorReading Unavailable() =>
        new(false, "test-unavailable", null, null, null);
}
