using System.Globalization;
using Channels;
using Channels.Logic;
using Channels.Timers;

namespace ChannelsTests.Timers;

[TestClass]
public class TimerEvaluationTests
{
    private static readonly Guid ChTrigger = new("00000000-0000-0000-0000-000000000063");
    private static readonly Guid ChOut     = new("00000000-0000-0000-0000-000000000010");
    private static readonly Guid ChOut2    = new("00000000-0000-0000-0000-000000000014");

    // -------------------------------------------------------------------------
    // Fakes
    // -------------------------------------------------------------------------

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset current;

        public FakeTimeProvider(DateTimeOffset start) => current = start;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan amount) => current = current.Add(amount);
    }

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private TimerMemoryRepository timerRepo = null!;
    private ChannelMemoryRepository channelRepo = null!;
    private ChannelDefinitionMemoryRepository channelDefRepo = null!;
    private StatementMemoryRepository statementRepo = null!;
    private FakeTimeProvider timeProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        timerRepo = new TimerMemoryRepository();
        channelRepo = new ChannelMemoryRepository();
        channelDefRepo = new ChannelDefinitionMemoryRepository();
        statementRepo = new StatementMemoryRepository();
        timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    private TimerEvaluation CreateEvaluation() =>
        new(timerRepo, channelRepo, channelDefRepo, statementRepo, timeProvider);

    // Always-true / always-false statements for controlling start/stop conditions.
    private static StatementDefinition AlwaysTrueStatement(int id) =>
        new() { Id = id, ActivateComparisons = [[new ComparisonDefinition { Id = id, ChannelId = ChTrigger, Logic = LogicType.True }]] };

    private static StatementDefinition AlwaysFalseStatement(int id) =>
        new() { Id = id, ActivateComparisons = [[new ComparisonDefinition { Id = id, ChannelId = ChTrigger, Logic = LogicType.False }]] };

    // A basic count-up timer with no limits.
    private static TimerDefinition BasicTimer(int id = 1, Guid outputChId = default, int startStmtId = 1, int stopStmtId = 2)
    {
        return new TimerDefinition { Id = id, OutputChId = outputChId == default ? ChOut : outputChId, StartStatementId = startStmtId, StopStatementId = stopStmtId };
    }

    // Pre-seed a timer state where the start edge will fire on the next true evaluation.
    private void SetReadyToStart(int timerId = 1) =>
        timerRepo.SetState(new TimerState { Id = timerId, PreviousStartResult = false, PreviousStopResult = true });

    // Pre-seed a running timer state. stopEdgeReady=true means the next true stop evaluation fires the edge.
    private void SetRunning(int timerId, double startValue, bool stopEdgeReady = false) =>
        timerRepo.SetState(new TimerState
        {
            Id = timerId,
            Started = timeProvider.GetUtcNow(),
            StartValue = startValue,
            PreviousStartResult = true,
            PreviousStopResult = !stopEdgeReady,
        });

    private double ParseOutput(Guid channelId) =>
        double.Parse(channelRepo.Get(channelId).Value, CultureInfo.InvariantCulture);

    // -------------------------------------------------------------------------
    // Edge detection — start
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DefaultInitialState_StartConditionTrue_NoEdgeFires_TimerDoesNotStart()
    {
        // PreviousStartResult defaults to true → true→true is NOT a false→true edge.
        timerRepo.AddTimer(BasicTimer());
        statementRepo.Set(AlwaysTrueStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));

        await CreateEvaluation().UpdateTimersAsync();

        Assert.IsNull(timerRepo.GetState(1)?.Started);
    }

    [TestMethod]
    public async Task StartEdge_StartsTimer()
    {
        timerRepo.AddTimer(BasicTimer());
        statementRepo.Set(AlwaysTrueStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetReadyToStart();

        await CreateEvaluation().UpdateTimersAsync();

        Assert.IsNotNull(timerRepo.GetState(1)?.Started);
    }

    [TestMethod]
    public async Task StartEdge_ConditionStaysTrue_DoesNotRestartTimer()
    {
        // Once started, a sustained true start condition must NOT restart the timer.
        timerRepo.AddTimer(BasicTimer());
        statementRepo.Set(AlwaysTrueStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetReadyToStart();

        var evaluation = CreateEvaluation();
        await evaluation.UpdateTimersAsync();  // edge fires, timer starts

        var startedAt = timerRepo.GetState(1)!.Started;

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await evaluation.UpdateTimersAsync();  // start stays true → no edge (true→true)

        Assert.AreEqual(startedAt, timerRepo.GetState(1)!.Started);  // same start time
    }

    [TestMethod]
    public async Task StartEdge_AfterBeingFalse_RefiresEdge()
    {
        // Start → stop → start again should work (false→true fires again after going false).
        timerRepo.AddTimer(BasicTimer());
        statementRepo.Set(AlwaysTrueStatement(1));
        statementRepo.Set(AlwaysTrueStatement(2));  // stop always fires edge when ready
        SetReadyToStart();

        var evaluation = CreateEvaluation();
        await evaluation.UpdateTimersAsync();  // timer starts

        // Make stop edge fire to stop the timer
        timerRepo.SetState(new TimerState
        {
            Id = 1,
            Started = timerRepo.GetState(1)!.Started,
            StartValue = timerRepo.GetState(1)!.StartValue,
            PreviousStartResult = true,
            PreviousStopResult = false,  // ready for stop edge
        });
        await evaluation.UpdateTimersAsync();  // timer stops

        Assert.IsNull(timerRepo.GetState(1)!.Started);

        // Mark start as ready again (PreviousStartResult = false)
        timerRepo.SetState(new TimerState { Id = 1, PreviousStartResult = false, PreviousStopResult = true });
        await evaluation.UpdateTimersAsync();  // start fires again

        Assert.IsNotNull(timerRepo.GetState(1)!.Started);
    }

    // -------------------------------------------------------------------------
    // Start value
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task StartEdge_EnableStartSeconds_SetsOutputAndStartValueToStartSeconds()
    {
        var timer = BasicTimer();
        timer.EnableStartSeconds = true;
        timer.StartSeconds = 30;
        timerRepo.AddTimer(timer);
        statementRepo.Set(AlwaysTrueStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetReadyToStart();

        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(30.0, ParseOutput(ChOut), 0.001);
        Assert.AreEqual(30.0, timerRepo.GetState(1)!.StartValue, 0.001);
    }

    [TestMethod]
    public async Task StartEdge_NoEnableStartSeconds_ResumesFromCurrentChannelValue()
    {
        channelRepo.Set(ChOut, "15");
        timerRepo.AddTimer(BasicTimer());
        statementRepo.Set(AlwaysTrueStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetReadyToStart();

        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(15.0, timerRepo.GetState(1)!.StartValue, 0.001);
        Assert.AreEqual(15.0, ParseOutput(ChOut), 0.001);
    }

    [TestMethod]
    public async Task StartEdge_NoEnableStartSeconds_UnparsableChannelValue_DefaultsToZero()
    {
        channelRepo.Set(ChOut, "");
        timerRepo.AddTimer(BasicTimer());
        statementRepo.Set(AlwaysTrueStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetReadyToStart();

        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(0.0, timerRepo.GetState(1)!.StartValue, 0.001);
    }

    // -------------------------------------------------------------------------
    // Count direction
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task CountUp_OutputIncreasesOverTime()
    {
        timerRepo.AddTimer(BasicTimer());
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetRunning(timerId: 1, startValue: 0);

        timeProvider.Advance(TimeSpan.FromSeconds(10));
        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(10.0, ParseOutput(ChOut), 0.001);
    }

    [TestMethod]
    public async Task CountDown_OutputDecreasesOverTime()
    {
        var timer = BasicTimer();
        timer.CountDown = true;
        timerRepo.AddTimer(timer);
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetRunning(timerId: 1, startValue: 20);

        timeProvider.Advance(TimeSpan.FromSeconds(7));
        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(13.0, ParseOutput(ChOut), 0.001);
    }

    [TestMethod]
    public async Task CountUp_StartsFromNonZeroStartValue_CountsUpFromThere()
    {
        var timer = BasicTimer();
        timer.EnableStartSeconds = true;
        timer.StartSeconds = 50;
        timerRepo.AddTimer(timer);
        statementRepo.Set(AlwaysTrueStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetReadyToStart();

        var evaluation = CreateEvaluation();
        await evaluation.UpdateTimersAsync();       // starts at 50

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await evaluation.UpdateTimersAsync();       // 50 + 5

        Assert.AreEqual(55.0, ParseOutput(ChOut), 0.001);
    }

    // -------------------------------------------------------------------------
    // Stop edge
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task StopEdge_StopsTimer()
    {
        timerRepo.AddTimer(BasicTimer());
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysTrueStatement(2));
        SetRunning(timerId: 1, startValue: 0, stopEdgeReady: true);

        await CreateEvaluation().UpdateTimersAsync();

        Assert.IsNull(timerRepo.GetState(1)!.Started);
    }

    [TestMethod]
    public async Task StopEdge_EnableStopSeconds_SetsOutputToStopSeconds()
    {
        var timer = BasicTimer();
        timer.EnableStopSeconds = true;
        timer.StopSeconds = 99;
        timerRepo.AddTimer(timer);
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysTrueStatement(2));
        SetRunning(timerId: 1, startValue: 0, stopEdgeReady: true);

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual("99", channelRepo.Get(ChOut).Value);
    }

    [TestMethod]
    public async Task StopEdge_NoEnableStopSeconds_FreezesAtCalculatedValue()
    {
        timerRepo.AddTimer(BasicTimer());
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysTrueStatement(2));
        SetRunning(timerId: 1, startValue: 0, stopEdgeReady: true);

        timeProvider.Advance(TimeSpan.FromSeconds(7));
        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(7.0, ParseOutput(ChOut), 0.001);
        Assert.IsNull(timerRepo.GetState(1)!.Started);
    }

    [TestMethod]
    public async Task StopConditionTrue_NoPreviousEdge_TimerContinuesRunning()
    {
        // Stop condition is true, but PreviousStopResult=true → true→true is NOT a false→true edge.
        timerRepo.AddTimer(BasicTimer());
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysTrueStatement(2));
        SetRunning(timerId: 1, startValue: 0, stopEdgeReady: false);  // no edge

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await CreateEvaluation().UpdateTimersAsync();

        Assert.IsNotNull(timerRepo.GetState(1)!.Started);
        Assert.AreEqual(5.0, ParseOutput(ChOut), 0.001);
    }

    // -------------------------------------------------------------------------
    // Limits — count up
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task CountUp_NoLimit_CountsIndefinitely()
    {
        timerRepo.AddTimer(BasicTimer());  // RolloverSeconds = 0
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetRunning(timerId: 1, startValue: 0);

        timeProvider.Advance(TimeSpan.FromSeconds(1000));
        await CreateEvaluation(). UpdateTimersAsync();

        Assert.IsNotNull(timerRepo.GetState(1)!.Started);  // still running
        Assert.AreEqual(1000.0, ParseOutput(ChOut), 0.001);
    }

    [TestMethod]
    public async Task CountUp_ValueExactlyAtLimit_NotExceeded_ContinuesRunning()
    {
        // The spec says it exceeds at "10.000001", so exactly 10 does NOT trigger rollover.
        var timer = BasicTimer();
        timer.RolloverSeconds = 10;
        timer.EnableRollover = false;
        timerRepo.AddTimer(timer);
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetRunning(timerId: 1, startValue: 0);

        timeProvider.Advance(TimeSpan.FromSeconds(10));
        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(10.0, ParseOutput(ChOut), 0.001);
        Assert.IsNotNull(timerRepo.GetState(1)!.Started);  // still running
    }

    [TestMethod]
    public async Task CountUp_LimitExceeded_NoRollover_ClampsToLimitAndStops()
    {
        var timer = BasicTimer();
        timer.RolloverSeconds = 10;
        timer.EnableRollover = false;
        timerRepo.AddTimer(timer);
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetRunning(timerId: 1, startValue: 0);

        timeProvider.Advance(TimeSpan.FromSeconds(15));
        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(10.0, ParseOutput(ChOut), 0.001);
        Assert.IsNull(timerRepo.GetState(1)!.Started);  // stopped
    }

    [TestMethod]
    public async Task CountUp_LimitExceeded_RolloverEnabled_WrapsAroundAndContinues()
    {
        var timer = BasicTimer();
        timer.RolloverSeconds = 10;
        timer.EnableRollover = true;
        timerRepo.AddTimer(timer);
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetRunning(timerId: 1, startValue: 0);

        timeProvider.Advance(TimeSpan.FromSeconds(13));  // 13 % 10 = 3
        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(3.0, ParseOutput(ChOut), 0.001);
        Assert.IsNotNull(timerRepo.GetState(1)!.Started);  // still running
    }

    [TestMethod]
    public async Task CountUp_LimitExceeded_RolloverEnabled_MultipleWraps_CorrectValue()
    {
        var timer = BasicTimer();
        timer.RolloverSeconds = 10;
        timer.EnableRollover = true;
        timerRepo.AddTimer(timer);
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetRunning(timerId: 1, startValue: 0);

        timeProvider.Advance(TimeSpan.FromSeconds(27));  // 27 % 10 = 7
        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(7.0, ParseOutput(ChOut), 0.001);
    }

    // -------------------------------------------------------------------------
    // Limits — count down
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task CountDown_NoLimit_CountsBelowZeroIndefinitely()
    {
        var timer = BasicTimer();
        timer.CountDown = true;
        timerRepo.AddTimer(timer);  // RolloverSeconds = 0
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetRunning(timerId: 1, startValue: 5);

        timeProvider.Advance(TimeSpan.FromSeconds(20));
        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(-15.0, ParseOutput(ChOut), 0.001);
        Assert.IsNotNull(timerRepo.GetState(1)!.Started);  // still running
    }

    [TestMethod]
    public async Task CountDown_BelowZero_NoRollover_ClampsToZeroAndStops()
    {
        var timer = BasicTimer();
        timer.CountDown = true;
        timer.RolloverSeconds = 10;
        timer.EnableRollover = false;
        timerRepo.AddTimer(timer);
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetRunning(timerId: 1, startValue: 5);

        timeProvider.Advance(TimeSpan.FromSeconds(8));  // 5 - 8 = -3 → clamp to 0
        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(0.0, ParseOutput(ChOut), 0.001);
        Assert.IsNull(timerRepo.GetState(1)!.Started);  // stopped
    }

    [TestMethod]
    public async Task CountDown_BelowZero_RolloverEnabled_WrapsToHighLimitAndContinues()
    {
        var timer = BasicTimer();
        timer.CountDown = true;
        timer.RolloverSeconds = 10;
        timer.EnableRollover = true;
        timerRepo.AddTimer(timer);
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetRunning(timerId: 1, startValue: 3);

        // 3 - 5 = -2 → positive modulo: ((-2 % 10) + 10) % 10 = 8
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(8.0, ParseOutput(ChOut), 0.001);
        Assert.IsNotNull(timerRepo.GetState(1)!.Started);  // still running
    }

    [TestMethod]
    public async Task CountDown_BelowZero_RolloverEnabled_MultipleWraps_CorrectValue()
    {
        var timer = BasicTimer();
        timer.CountDown = true;
        timer.RolloverSeconds = 10;
        timer.EnableRollover = true;
        timerRepo.AddTimer(timer);
        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        SetRunning(timerId: 1, startValue: 3);

        // 3 - 25 = -22 → ((-22 % 10) + 10) % 10 = ((-2) + 10) % 10 = 8
        timeProvider.Advance(TimeSpan.FromSeconds(25));
        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(8.0, ParseOutput(ChOut), 0.001);
    }

    // -------------------------------------------------------------------------
    // Multiple timers
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task MultipleTimers_EachUpdatedIndependently()
    {
        var timer1 = new TimerDefinition { Id = 1, OutputChId = ChOut,  StartStatementId = 1, StopStatementId = 2 };
        var timer2 = new TimerDefinition { Id = 2, OutputChId = ChOut2, StartStatementId = 3, StopStatementId = 4, CountDown = true };
        timerRepo.AddTimer(timer1);
        timerRepo.AddTimer(timer2);

        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysFalseStatement(2));
        statementRepo.Set(AlwaysFalseStatement(3));
        statementRepo.Set(AlwaysFalseStatement(4));

        SetRunning(timerId: 1, startValue: 0);
        timerRepo.SetState(new TimerState { Id = 2, Started = timeProvider.GetUtcNow(), StartValue = 100, PreviousStartResult = true, PreviousStopResult = true });

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await CreateEvaluation().UpdateTimersAsync();

        Assert.AreEqual(5.0,  ParseOutput(ChOut),  0.001);
        Assert.AreEqual(95.0, ParseOutput(ChOut2), 0.001);
    }

    [TestMethod]
    public async Task MultipleTimers_OneStops_OtherContinues()
    {
        var timer1 = new TimerDefinition { Id = 1, OutputChId = ChOut,  StartStatementId = 1, StopStatementId = 2 };
        var timer2 = new TimerDefinition { Id = 2, OutputChId = ChOut2, StartStatementId = 3, StopStatementId = 4 };
        timerRepo.AddTimer(timer1);
        timerRepo.AddTimer(timer2);

        statementRepo.Set(AlwaysFalseStatement(1));
        statementRepo.Set(AlwaysTrueStatement(2));
        statementRepo.Set(AlwaysFalseStatement(3));
        statementRepo.Set(AlwaysFalseStatement(4));

        SetRunning(timerId: 1, startValue: 0, stopEdgeReady: true);
        SetRunning(timerId: 2, startValue: 0, stopEdgeReady: false);

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        await CreateEvaluation().UpdateTimersAsync();

        Assert.IsNull(timerRepo.GetState(1)!.Started);
        Assert.IsNotNull(timerRepo.GetState(2)!.Started);
        Assert.AreEqual(5.0, ParseOutput(ChOut2), 0.001);
    }

    // -------------------------------------------------------------------------
    // State persistence
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task TimerNotRunning_NoEdge_StateStillPersisted()
    {
        // Even when nothing happens, PreviousStartResult / PreviousStopResult must be saved.
        timerRepo.AddTimer(BasicTimer());
        statementRepo.Set(AlwaysTrueStatement(1));   // start stays true
        statementRepo.Set(AlwaysFalseStatement(2));

        // Default state: PreviousStartResult=true, so start=true doesn't fire.
        await CreateEvaluation().UpdateTimersAsync();

        var state = timerRepo.GetState(1)!;
        Assert.IsTrue(state.PreviousStartResult);   // persisted as true
        Assert.IsFalse(state.PreviousStopResult);   // persisted as false
    }
}


