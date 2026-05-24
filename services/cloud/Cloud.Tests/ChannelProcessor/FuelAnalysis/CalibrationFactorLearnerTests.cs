using ChannelProcessor.FuelAnalysis.Calibration;
using ChannelProcessor.FuelAnalysis.State;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cloud.Tests.ChannelProcessor.FuelAnalysis;

[TestClass]
public class CalibrationFactorLearnerTests
{
    private const int TeamId = 1;
    private const string CarNumber = "42";
    private const int RaceId = 7;
    private static readonly DateTime ClosedAt = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private RaceManagementContext _db = null!;
    private Mock<ICalibrationFactorReader> _reader = null!;
    private CalibrationFactorLearner _learner = null!;

    [TestInitialize]
    public void Setup()
    {
        // Unique InMemory DB per test → MethodLevel parallelism is safe.
        var options = new DbContextOptionsBuilder<RaceManagementContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new RaceManagementContext(options);

        _reader = new Mock<ICalibrationFactorReader>();
        _reader.Setup(r => r.GetLatestAsync(TeamId, CarNumber, It.IsAny<CancellationToken>()))
               .ReturnsAsync((CalibrationFactor?)null);
        _reader.Setup(r => r.InvalidateAsync(TeamId, CarNumber, It.IsAny<CancellationToken>()))
               .Returns(Task.CompletedTask);

        _learner = new CalibrationFactorLearner(_reader.Object, new Mock<ILogger<CalibrationFactorLearner>>().Object);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    [TestMethod]
    public async Task Learn_NotResetConfirmed_NoWriteNoInvalidate()
    {
        var state = ValidState(ecuResetState: EcuResetState.ResetInferred);

        await _learner.LearnAsync(_db, TeamId, CarNumber, RaceId, state, ClosedAt, default);

        Assert.AreEqual(0, await _db.CalibrationFactors.CountAsync());
        _reader.Verify(r => r.InvalidateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Learn_NoFlowMeterBaseline_NoWrite()
    {
        var state = ValidState();
        state.CurrentWindowFlowMeterFuelUsedAtOpen = null;

        await _learner.LearnAsync(_db, TeamId, CarNumber, RaceId, state, ClosedAt, default);

        Assert.AreEqual(0, await _db.CalibrationFactors.CountAsync());
    }

    [TestMethod]
    public async Task Learn_FlowMeterUsedTooSmall_NoWrite()
    {
        // 10.05 - 10.0 = 0.05 ≤ 0.1 → skip.
        var state = ValidState(fmAtOpen: 10.0, fmCurrent: 10.05, ecuUsed: 5);

        await _learner.LearnAsync(_db, TeamId, CarNumber, RaceId, state, ClosedAt, default);

        Assert.AreEqual(0, await _db.CalibrationFactors.CountAsync());
    }

    [TestMethod]
    public async Task Learn_EcuUsedTooSmall_NoWrite()
    {
        var state = ValidState(fmAtOpen: 10.0, fmCurrent: 15.0, ecuUsed: 0.05);

        await _learner.LearnAsync(_db, TeamId, CarNumber, RaceId, state, ClosedAt, default);

        Assert.AreEqual(0, await _db.CalibrationFactors.CountAsync());
    }

    [TestMethod]
    public async Task Learn_ObservedFactorBelowMin_NoWrite()
    {
        // observed = ECU 1 / FM 5 = 0.2 → < 0.5.
        var state = ValidState(fmAtOpen: 0, fmCurrent: 5, ecuUsed: 1);

        await _learner.LearnAsync(_db, TeamId, CarNumber, RaceId, state, ClosedAt, default);

        Assert.AreEqual(0, await _db.CalibrationFactors.CountAsync());
    }

    [TestMethod]
    public async Task Learn_ObservedFactorAboveMax_NoWrite()
    {
        // observed = ECU 30 / FM 5 = 6 → > 2.0.
        var state = ValidState(fmAtOpen: 0, fmCurrent: 5, ecuUsed: 30);

        await _learner.LearnAsync(_db, TeamId, CarNumber, RaceId, state, ClosedAt, default);

        Assert.AreEqual(0, await _db.CalibrationFactors.CountAsync());
    }

    [TestMethod]
    public async Task Learn_ManualOverrideExists_NoWrite_StillNoInvalidate()
    {
        var manual = new CalibrationFactor
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            Value = 1.1,
            Source = CalibrationFactorSource.ManualOverride,
            EffectiveAt = ClosedAt - TimeSpan.FromDays(1),
        };
        _reader.Setup(r => r.GetLatestAsync(TeamId, CarNumber, It.IsAny<CancellationToken>()))
               .ReturnsAsync(manual);

        var state = ValidState(); // observed = 5/5 = 1.0

        await _learner.LearnAsync(_db, TeamId, CarNumber, RaceId, state, ClosedAt, default);

        Assert.AreEqual(0, await _db.CalibrationFactors.CountAsync());
        _reader.Verify(r => r.InvalidateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Learn_NoPriorFactor_BootstrapsWithObservedValue()
    {
        // observed = ECU 4 / FM 5 = 0.8.
        var state = ValidState(fmAtOpen: 0, fmCurrent: 5, ecuUsed: 4);

        await _learner.LearnAsync(_db, TeamId, CarNumber, RaceId, state, ClosedAt, default);

        var rows = await _db.CalibrationFactors.ToListAsync();
        Assert.HasCount(1, rows);
        Assert.AreEqual(0.8, rows[0].Value, 1e-9);
        Assert.AreEqual(CalibrationFactorSource.Learned, rows[0].Source);
        Assert.AreEqual(TeamId, rows[0].TeamId);
        Assert.AreEqual(CarNumber, rows[0].CarNumber);
        Assert.AreEqual(RaceId, rows[0].RaceId);
        Assert.AreEqual(ClosedAt, rows[0].EffectiveAt);
        _reader.Verify(r => r.InvalidateAsync(TeamId, CarNumber, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Learn_WithPriorLearned_AppliesEmaSmoothing()
    {
        var prior = new CalibrationFactor
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            Value = 1.0,
            Source = CalibrationFactorSource.Learned,
            EffectiveAt = ClosedAt - TimeSpan.FromHours(1),
        };
        _reader.Setup(r => r.GetLatestAsync(TeamId, CarNumber, It.IsAny<CancellationToken>()))
               .ReturnsAsync(prior);

        // observed = 0.8 → new = 0.7 * 1.0 + 0.3 * 0.8 = 0.94.
        var state = ValidState(fmAtOpen: 0, fmCurrent: 5, ecuUsed: 4);

        await _learner.LearnAsync(_db, TeamId, CarNumber, RaceId, state, ClosedAt, default);

        var written = await _db.CalibrationFactors.SingleAsync();
        Assert.AreEqual(0.94, written.Value, 1e-9);
        _reader.Verify(r => r.InvalidateAsync(TeamId, CarNumber, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Learn_WithPriorReset_AppliesEmaSmoothing_NotBlocked()
    {
        // Reset is not ManualOverride → learning continues.
        var prior = new CalibrationFactor
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            Value = 1.0,
            Source = CalibrationFactorSource.Reset,
            EffectiveAt = ClosedAt - TimeSpan.FromHours(1),
        };
        _reader.Setup(r => r.GetLatestAsync(TeamId, CarNumber, It.IsAny<CancellationToken>()))
               .ReturnsAsync(prior);

        var state = ValidState(fmAtOpen: 0, fmCurrent: 5, ecuUsed: 5); // observed 1.0

        await _learner.LearnAsync(_db, TeamId, CarNumber, RaceId, state, ClosedAt, default);

        var written = await _db.CalibrationFactors.SingleAsync();
        // 0.7 * 1.0 + 0.3 * 1.0 = 1.0
        Assert.AreEqual(1.0, written.Value, 1e-9);
    }

    // ---------------- Helpers ----------------

    private static CarFuelState ValidState(
        EcuResetState ecuResetState = EcuResetState.ResetConfirmed,
        double fmAtOpen = 0,
        double fmCurrent = 5,
        double ecuUsed = 5) =>
        new()
        {
            CurrentWindowEcuResetState = ecuResetState,
            CurrentWindowFlowMeterFuelUsedAtOpen = fmAtOpen,
            LastFuelUsedValue = fmCurrent,
            LastTripFuelValue = ecuUsed,
        };
}
