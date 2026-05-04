using Channels;
using Channels.Counters;

namespace ChannelsTests.Counters;

[TestClass]
public class CounterEvaluationTests
{
    private static readonly Guid Ch1   = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Ch2   = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Ch3   = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid ChOut  = new("00000000-0000-0000-0000-000000000010");
    private static readonly Guid ChOut2 = new("00000000-0000-0000-0000-000000000014");
    private static readonly Guid Counter1 = new("00000000-0000-0000-0000-000000001001");
    private static readonly Guid Counter2 = new("00000000-0000-0000-0000-000000001002");

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private CounterMemoryRepository counterRepo = null!;
    private ChannelMemoryRepository channelRepo = null!;

    [TestInitialize]
    public void Setup()
    {
        counterRepo = new CounterMemoryRepository();
        channelRepo = new ChannelMemoryRepository();
    }

    private CounterEvaluation CreateEvaluation() =>
        new(counterRepo, channelRepo);

    private static CounterDefinition BasicCounter(Guid id = default, Guid outputChId = default, Guid upChId = default, Guid downChId = default, Guid resetChId = default)
    {
        return new CounterDefinition
        {
            Id = id == default ? Counter1 : id,
            Name = "Counter",
            OutputChId = outputChId == default ? ChOut : outputChId,
            UpChId     = upChId    == default ? Ch1   : upChId,
            DownChId   = downChId  == default ? Ch2   : downChId,
            ResetChId  = resetChId == default ? Ch3   : resetChId,
            MinValue = 0,
            MaxValue = 100,
            StartValue = 0,
        };
    }

    private int GetOutput(Guid channelId) =>
        int.Parse(channelRepo.Get(channelId).Value);

    // -------------------------------------------------------------------------
    // Initialization
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task FirstRun_InitializesToStartValue()
    {
        var counter = BasicCounter();
        counter.StartValue = 10;
        counterRepo.Add(counter);
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        await CreateEvaluation().UpdateCountersAsync();

        Assert.AreEqual(10, GetOutput(ChOut));
    }

    [TestMethod]
    public async Task SecondRun_DoesNotReinitialize()
    {
        var counter = BasicCounter();
        counter.StartValue = 10;
        counterRepo.Add(counter);
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch1, "1");
        await eval.UpdateCountersAsync();

        Assert.AreEqual(11, GetOutput(ChOut));
    }

    // -------------------------------------------------------------------------
    // Rising-edge detection — Up channel
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task UpEdge_SignalGoesFromZeroToNonZero_Increments()
    {
        counterRepo.Add(BasicCounter());
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch1, "1");
        await eval.UpdateCountersAsync();

        Assert.AreEqual(1, GetOutput(ChOut));
    }

    [TestMethod]
    public async Task UpEdge_SignalStaysNonZero_DoesNotIncrementAgain()
    {
        counterRepo.Add(BasicCounter());
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch1, "1");
        await eval.UpdateCountersAsync();

        await eval.UpdateCountersAsync();

        Assert.AreEqual(1, GetOutput(ChOut));
    }

    [TestMethod]
    public async Task UpEdge_SignalReturnsToZeroThenRises_IncrementsAgain()
    {
        counterRepo.Add(BasicCounter());
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch1, "1");
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch1, "0");
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch1, "5");
        await eval.UpdateCountersAsync();

        Assert.AreEqual(2, GetOutput(ChOut));
    }

    // -------------------------------------------------------------------------
    // Rising-edge detection — Down channel
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DownEdge_Decrements()
    {
        var counter = BasicCounter();
        counter.StartValue = 5;
        counterRepo.Add(counter);
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch2, "1");
        await eval.UpdateCountersAsync();

        Assert.AreEqual(4, GetOutput(ChOut));
    }

    [TestMethod]
    public async Task DownEdge_StaysNonZero_DoesNotDecrementAgain()
    {
        var counter = BasicCounter();
        counter.StartValue = 5;
        counterRepo.Add(counter);
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch2, "1");
        await eval.UpdateCountersAsync();
        await eval.UpdateCountersAsync();

        Assert.AreEqual(4, GetOutput(ChOut));
    }

    // -------------------------------------------------------------------------
    // Rising-edge detection — Reset channel
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task ResetEdge_ResetsToStartValue()
    {
        var counter = BasicCounter();
        counter.StartValue = 0;
        counterRepo.Add(counter);
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch1, "1");
        await eval.UpdateCountersAsync();
        channelRepo.Set(Ch1, "0");
        await eval.UpdateCountersAsync();
        channelRepo.Set(Ch1, "1");
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch3, "1");
        await eval.UpdateCountersAsync();

        Assert.AreEqual(0, GetOutput(ChOut));
    }

    [TestMethod]
    public async Task ResetEdge_TakesPriorityOverUpAndDown()
    {
        var counter = BasicCounter();
        counter.StartValue = 50;
        counterRepo.Add(counter);
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch1, "1");
        channelRepo.Set(Ch2, "1");
        channelRepo.Set(Ch3, "1");
        await eval.UpdateCountersAsync();

        Assert.AreEqual(50, GetOutput(ChOut));
    }

    // -------------------------------------------------------------------------
    // Limits — no roll
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Increment_AtMaxValue_NoRoll_ClampsToMax()
    {
        var counter = BasicCounter();
        counter.StartValue = 100;
        counter.MaxValue = 100;
        counter.RollAtLimit = false;
        counterRepo.Add(counter);
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch1, "1");
        await eval.UpdateCountersAsync();

        Assert.AreEqual(100, GetOutput(ChOut));
    }

    [TestMethod]
    public async Task Decrement_AtMinValue_NoRoll_ClampsToMin()
    {
        var counter = BasicCounter();
        counter.StartValue = 0;
        counter.MinValue = 0;
        counter.RollAtLimit = false;
        counterRepo.Add(counter);
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch2, "1");
        await eval.UpdateCountersAsync();

        Assert.AreEqual(0, GetOutput(ChOut));
    }

    // -------------------------------------------------------------------------
    // Limits — roll at limit
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Increment_AtMaxValue_RollAtLimit_WrapsToMin()
    {
        var counter = BasicCounter();
        counter.StartValue = 100;
        counter.MinValue = 0;
        counter.MaxValue = 100;
        counter.RollAtLimit = true;
        counterRepo.Add(counter);
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch1, "1");
        await eval.UpdateCountersAsync();

        Assert.AreEqual(0, GetOutput(ChOut));
    }

    [TestMethod]
    public async Task Decrement_AtMinValue_RollAtLimit_WrapsToMax()
    {
        var counter = BasicCounter();
        counter.StartValue = 0;
        counter.MinValue = 0;
        counter.MaxValue = 100;
        counter.RollAtLimit = true;
        counterRepo.Add(counter);
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch2, "1");
        await eval.UpdateCountersAsync();

        Assert.AreEqual(100, GetOutput(ChOut));
    }

    // -------------------------------------------------------------------------
    // Unconfigured channels (Guid.Empty)
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task UpChannelNotConfigured_NeverIncrements()
    {
        var counter = BasicCounter();
        counter.UpChId = Guid.Empty;
        counter.StartValue = 5;
        counterRepo.Add(counter);
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        Assert.AreEqual(5, GetOutput(ChOut));
    }

    [TestMethod]
    public async Task DownChannelNotConfigured_NeverDecrements()
    {
        var counter = BasicCounter();
        counter.DownChId = Guid.Empty;
        counter.StartValue = 5;
        counterRepo.Add(counter);
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        Assert.AreEqual(5, GetOutput(ChOut));
    }

    // -------------------------------------------------------------------------
    // Up and Down in the same cycle
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task UpAndDownEdgeBothFire_NetEffectIsZero()
    {
        var counter = BasicCounter();
        counter.StartValue = 50;
        counterRepo.Add(counter);
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch1, "1");
        channelRepo.Set(Ch2, "1");
        await eval.UpdateCountersAsync();

        Assert.AreEqual(50, GetOutput(ChOut));
    }

    // -------------------------------------------------------------------------
    // Multiple counters
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task MultipleCounters_IndependentlyEvaluated()
    {
        var c1 = new CounterDefinition { Id = Counter1, Name = "Counter1", OutputChId = ChOut,  UpChId = Ch1,       DownChId = Guid.Empty, ResetChId = Guid.Empty, MinValue = 0, MaxValue = 100, StartValue = 0 };
        var c2 = new CounterDefinition { Id = Counter2, Name = "Counter2", OutputChId = ChOut2, UpChId = Guid.Empty, DownChId = Ch2,       ResetChId = Guid.Empty, MinValue = 0, MaxValue = 100, StartValue = 50 };
        counterRepo.Add(c1);
        counterRepo.Add(c2);
        channelRepo.Set(Ch1, "0");
        channelRepo.Set(Ch2, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(Ch1, "1");
        channelRepo.Set(Ch2, "1");
        await eval.UpdateCountersAsync();

        Assert.AreEqual(1,  GetOutput(ChOut));
        Assert.AreEqual(49, GetOutput(ChOut2));
    }

    // -------------------------------------------------------------------------
    // State persistence
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task EdgeTrackingState_PersistedBetweenCalls()
    {
        counterRepo.Add(BasicCounter());
        channelRepo.Set(Ch1, "1");
        channelRepo.Set(Ch2, "0");
        channelRepo.Set(Ch3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        var state = counterRepo.GetState(Counter1)!;
        Assert.IsFalse(state.PreviousUpWasZero);
        Assert.IsTrue(state.PreviousDownWasZero);
    }
}
