using ChannelProcessor.FuelAnalysis.Estimators;
using ChannelProcessor.FuelAnalysis.Pace;
using ChannelProcessor.FuelAnalysis.Reconciler;
using ChannelProcessor.FuelAnalysis.State;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Common.FuelAnalysis;

namespace Cloud.Tests.ChannelProcessor.FuelAnalysis;

[TestClass]
public class FuelReconcilerTests
{
    private const int TeamId = 1;
    private const string CarNumber = "42";
    private const int RaceId = 7;
    private static readonly DateTime NowUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private OutlierDebouncer _debouncer = null!;
    private RateModel _rateModel = null!;
    private DriverPaceCalculator _pace = null!;
    private CarFuelConfig _config = null!;

    [TestInitialize]
    public void Setup()
    {
        _debouncer = new OutlierDebouncer();
        _rateModel = new RateModel();
        _pace = new DriverPaceCalculator();
        _config = new CarFuelConfig { TankCapacityGallons = 20, DefaultConsumptionGalPerMin = 0.1 };
    }

    [TestMethod]
    public void Build_NoEstimators_PrimaryUnavailable()
    {
        var reconciler = new FuelReconciler([], _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, new CarFuelState(), _config, null, NowUtc);

        Assert.AreEqual("unavailable", snap.Primary.Source);
        Assert.IsNull(snap.Primary.RangeGallons);
        Assert.IsNull(snap.Primary.RangeMinutes);
        Assert.IsNull(snap.HighConfidence.RangeGallons);
    }

    [TestMethod]
    public void Build_AllEstimatorsUnavailable_PrimaryUnavailable_ButReadoutsStillEmitted()
    {
        var reconciler = new FuelReconciler(
            [new FakeEstimator("ecu", Unavailable("not ready"))],
            _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, new CarFuelState(), _config, null, NowUtc);

        Assert.AreEqual("unavailable", snap.Primary.Source);
        Assert.HasCount(1, snap.Estimators);
        Assert.IsFalse(snap.Estimators[0].Available);
        Assert.AreEqual("not ready", snap.Estimators[0].Reason);
    }

    [TestMethod]
    public void Build_SingleAvailableEstimator_SourceIsThatEstimator_RangeMatches()
    {
        var reconciler = new FuelReconciler(
            [new FakeEstimator("ecu", Ready(range: 12, sigma: 0.5, baseRate: 0.1))],
            _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, new CarFuelState(), _config, null, NowUtc);

        Assert.AreEqual("ecu", snap.Primary.Source);
        Assert.AreEqual(12, snap.Primary.RangeGallons!.Value, 1e-9);
        Assert.AreEqual(12.0 / 0.1, snap.Primary.RangeMinutes!.Value, 1e-9);
        Assert.IsGreaterThan(0, snap.Primary.Confidence);
    }

    [TestMethod]
    public void Build_SingleAvailable_SubNameStrippedForSource()
    {
        // "flowmeter.raw" → source "flowmeter".
        var reconciler = new FuelReconciler(
            [new FakeEstimator("flowmeter.raw", Ready(10, 1, 0.1))],
            _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, new CarFuelState(), _config, null, NowUtc);

        Assert.AreEqual("flowmeter", snap.Primary.Source);
    }

    [TestMethod]
    public void Build_TwoAvailableEstimators_Source_IsBlended_InverseVarianceWeighted()
    {
        // ecu = 10 ± 1 (weight 1), flowmeter = 14 ± 0.5 (weight 4)
        // Blended gallons = (1*10 + 4*14) / 5 = 13.2.
        var reconciler = new FuelReconciler(
            [
                new FakeEstimator("ecu", Ready(10, 1, 0.1)),
                new FakeEstimator("flowmeter.raw", Ready(14, 0.5, 0.1)),
            ],
            _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, new CarFuelState(), _config, null, NowUtc);

        Assert.AreEqual("blended", snap.Primary.Source);
        Assert.AreEqual(13.2, snap.Primary.RangeGallons!.Value, 1e-9);
        Assert.AreEqual(4.0, snap.Reconciler.SpreadGallons!.Value, 1e-9);
        Assert.AreEqual(0, snap.Reconciler.OutlierCount);
    }

    [TestMethod]
    public void Build_OutlierCommitted_ExcludedFromBlend()
    {
        // pre-commit pitfill as an outlier so the debouncer keeps the flag.
        var state = new CarFuelState
        {
            OutlierDebounce =
            {
                ["pitfill"] = new OutlierDebounceEntry { IsOutlier = true, PendingFlipSince = null },
            },
        };

        var reconciler = new FuelReconciler(
            [
                new FakeEstimator("ecu", Ready(10, 1, 0.1)),
                new FakeEstimator("flowmeter.raw", Ready(10, 1, 0.1)),
                new FakeEstimator("pitfill", Ready(30, 1, 0.1)),
            ],
            _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, state, _config, null, NowUtc);

        // Blend should be (10 + 10)/2 = 10 — pitfill excluded.
        Assert.AreEqual(10, snap.Primary.RangeGallons!.Value, 1e-9);
        Assert.AreEqual(1, snap.Reconciler.OutlierCount);
        var pitfillReadout = snap.Estimators.Single(e => e.Name == "pitfill");
        Assert.IsTrue(pitfillReadout.IsOutlier);
    }

    [TestMethod]
    public void Build_HighConfidence_ClampedToZeroForHugeSigma()
    {
        var reconciler = new FuelReconciler(
            [new FakeEstimator("ecu", Ready(0.1, 100, 0.1))],
            _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, new CarFuelState(), _config, null, NowUtc);

        Assert.AreEqual(0, snap.HighConfidence.RangeGallons!.Value, 1e-9);
        Assert.AreEqual(0.98, snap.HighConfidenceThreshold);
    }

    [TestMethod]
    public void Build_HighConfidence_BelowPrimary_ForNormalSigma()
    {
        var reconciler = new FuelReconciler(
            [new FakeEstimator("ecu", Ready(12, 0.5, 0.1))],
            _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, new CarFuelState(), _config, null, NowUtc);

        Assert.IsNotNull(snap.HighConfidence.RangeGallons);
        Assert.IsLessThan(snap.Primary.RangeGallons!.Value, snap.HighConfidence.RangeGallons!.Value);
        Assert.IsGreaterThanOrEqualTo(0, snap.HighConfidence.RangeGallons!.Value);
    }

    [TestMethod]
    public void Build_BypassRateEstimator_UsesItsOwnRateNotRateModel()
    {
        // Pace would clamp to 1.05; bypass=true means we must NOT apply that to the rate.
        var state = new CarFuelState
        {
            SessionLapTimes = { 100 },
            RecentLapTimes = { 50 },
        };

        var reconciler = new FuelReconciler(
            [new FakeEstimator("throttle.grid", Ready(10, 1, 0.2), bypassesRateModel: true)],
            _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, state, _config, null, NowUtc);

        // Range minutes = 10 / 0.2 = 50 — NOT 10 / (0.2 * 1.05).
        Assert.AreEqual(50, snap.Primary.RangeMinutes!.Value, 1e-9);
        Assert.AreEqual(1.05, snap.Reconciler.PaceMultiplier, 1e-9);
    }

    [TestMethod]
    public void Build_RateModelEstimator_DivisorScaledByPaceMultiplier()
    {
        var state = new CarFuelState
        {
            SessionLapTimes = { 100 },
            RecentLapTimes = { 50 }, // pace clamps to 1.05
        };

        var reconciler = new FuelReconciler(
            [new FakeEstimator("ecu", Ready(10, 1, 0.2))],
            _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, state, _config, null, NowUtc);

        // effective rate = 0.2 * 1.05 = 0.21; range = 10 / 0.21.
        Assert.AreEqual(10.0 / 0.21, snap.Primary.RangeMinutes!.Value, 1e-9);
    }

    [TestMethod]
    public void Build_CalibrationFactorMetadata_FlowsToReconcilerDetails()
    {
        var cal = new CalibrationFactor
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            Value = 1.07,
            Source = CalibrationFactorSource.ManualOverride,
            EffectiveAt = NowUtc - TimeSpan.FromMinutes(5),
        };
        var reconciler = new FuelReconciler(
            [new FakeEstimator("ecu", Ready(10, 1, 0.1))],
            _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, new CarFuelState(), _config, cal, NowUtc);

        Assert.AreEqual(1.07, snap.Reconciler.FlowMeterCalibrationFactor);
        Assert.AreEqual("manual_override", snap.Reconciler.FlowMeterCalibrationFactorSource);
    }

    [TestMethod]
    public void Build_FuelWindowDetails_PopulatedFromState()
    {
        var openedAt = NowUtc - TimeSpan.FromMinutes(30);
        var state = new CarFuelState
        {
            OpenFuelWindowId = 99,
            OpenFuelWindowOpenedAt = openedAt,
            CurrentWindowEnteredFuelGallons = 8.5,
            CurrentWindowFlowMeterFuelUsedAtOpen = 100,
            LastFuelUsedValue = 103.25,
        };
        var reconciler = new FuelReconciler([], _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, state, _config, null, NowUtc);

        Assert.AreEqual(99, snap.FuelWindow.Id);
        Assert.AreEqual(openedAt, snap.FuelWindow.OpenedAtUtc);
        Assert.AreEqual(30, snap.FuelWindow.ElapsedMinutes, 1e-9);
        Assert.AreEqual(8.5, snap.FuelWindow.EnteredFuelGallons);
        Assert.AreEqual(3.25, snap.FuelWindow.GallonsUsedInWindow!.Value, 1e-9);
    }

    [TestMethod]
    public void Build_LapsConversion_AppliedWhenRecentAvgLapAvailable()
    {
        var state = new CarFuelState
        {
            SessionLapTimes = { 60 },
            RecentLapTimes = { 60 }, // recent avg = 60s
        };
        var reconciler = new FuelReconciler(
            [new FakeEstimator("ecu", Ready(6, 0.5, 0.1))],
            _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, state, _config, null, NowUtc);

        // range minutes = 6 / 0.1 = 60. 60 minutes ÷ (60s/lap) = 60 laps.
        Assert.AreEqual(60, snap.Primary.RangeMinutes!.Value, 1e-9);
        Assert.AreEqual(60, snap.Primary.RangeLaps!.Value, 1e-9);
    }

    [TestMethod]
    public void Build_TopLevelMetadata_AlwaysSetOnSnapshot()
    {
        var reconciler = new FuelReconciler([], _debouncer, _rateModel, _pace);

        var snap = reconciler.Build(TeamId, CarNumber, RaceId, new CarFuelState(), _config, null, NowUtc);

        Assert.AreEqual(TeamId, snap.TeamId);
        Assert.AreEqual(CarNumber, snap.CarNumber);
        Assert.AreEqual(RaceId, snap.RaceId);
        Assert.AreEqual(NowUtc, snap.AsOfUtc);
    }

    // ---------------- Helpers ----------------

    private static EstimatorReading Ready(double range, double sigma, double baseRate) =>
        new(true, null, range, sigma, baseRate);

    private static EstimatorReading Unavailable(string reason) =>
        new(false, reason, null, null, null);

    private sealed class FakeEstimator(string name, EstimatorReading reading, bool bypassesRateModel = false) : IFuelEstimator
    {
        public string Name { get; } = name;
        public bool BypassesRateModel { get; } = bypassesRateModel;
        public EstimatorReading Compute(in EstimatorContext context) => reading;
    }
}
