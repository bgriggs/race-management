using Channels;
using Channels.Counters;

namespace ChannelsTests.Counters;

[TestClass]
public class CounterEvaluationTests
{
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

    private static CounterDefinition BasicCounter(int id = 1, int outputChId = 10, int upChId = 1, int downChId = 2, int resetChId = 3) =>
        new()
        {
            Id = id,
            OutputChId = outputChId,
            UpChId = upChId,
            DownChId = downChId,
            ResetChId = resetChId,
            MinValue = 0,
            MaxValue = 100,
            StartValue = 0,
        };

    private int GetOutput(int channelId) =>
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
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        await CreateEvaluation().UpdateCountersAsync();

        Assert.AreEqual(10, GetOutput(10));
    }

    [TestMethod]
    public async Task SecondRun_DoesNotReinitialize()
    {
        var counter = BasicCounter();
        counter.StartValue = 10;
        counterRepo.Add(counter);
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init to 10

        channelRepo.Set(1, "1");                    // up edge
        await eval.UpdateCountersAsync();           // should increment to 11, not reinit to 10

        Assert.AreEqual(11, GetOutput(10));
    }

    // -------------------------------------------------------------------------
    // Rising-edge detection — Up channel
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task UpEdge_SignalGoesFromZeroToNonZero_Increments()
    {
        counterRepo.Add(BasicCounter());
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init, up=0

        channelRepo.Set(1, "1");                    // rising edge
        await eval.UpdateCountersAsync();

        Assert.AreEqual(1, GetOutput(10));
    }

    [TestMethod]
    public async Task UpEdge_SignalStaysNonZero_DoesNotIncrementAgain()
    {
        counterRepo.Add(BasicCounter());
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init

        channelRepo.Set(1, "1");
        await eval.UpdateCountersAsync();           // edge fires → 1

        await eval.UpdateCountersAsync();           // still 1, no edge → stays 1

        Assert.AreEqual(1, GetOutput(10));
    }

    [TestMethod]
    public async Task UpEdge_SignalReturnsToZeroThenRises_IncrementsAgain()
    {
        counterRepo.Add(BasicCounter());
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init

        channelRepo.Set(1, "1");
        await eval.UpdateCountersAsync();           // → 1

        channelRepo.Set(1, "0");
        await eval.UpdateCountersAsync();           // back to zero, no increment

        channelRepo.Set(1, "5");                    // any non-zero triggers edge
        await eval.UpdateCountersAsync();           // → 2

        Assert.AreEqual(2, GetOutput(10));
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
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init to 5

        channelRepo.Set(2, "1");                    // down rising edge
        await eval.UpdateCountersAsync();           // → 4

        Assert.AreEqual(4, GetOutput(10));
    }

    [TestMethod]
    public async Task DownEdge_StaysNonZero_DoesNotDecrementAgain()
    {
        var counter = BasicCounter();
        counter.StartValue = 5;
        counterRepo.Add(counter);
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        channelRepo.Set(2, "1");
        await eval.UpdateCountersAsync();           // → 4
        await eval.UpdateCountersAsync();           // still 1, no edge → 4

        Assert.AreEqual(4, GetOutput(10));
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
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init to 0

        channelRepo.Set(1, "1");
        await eval.UpdateCountersAsync();           // → 1
        channelRepo.Set(1, "0");
        await eval.UpdateCountersAsync();
        channelRepo.Set(1, "1");
        await eval.UpdateCountersAsync();           // → 2

        channelRepo.Set(1, "0");
        channelRepo.Set(3, "1");                    // reset edge
        await eval.UpdateCountersAsync();           // → 0 (StartValue)

        Assert.AreEqual(0, GetOutput(10));
    }

    [TestMethod]
    public async Task ResetEdge_TakesPriorityOverUpAndDown()
    {
        var counter = BasicCounter();
        counter.StartValue = 50;
        counterRepo.Add(counter);
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init to 50

        // All three rising edges fire simultaneously
        channelRepo.Set(1, "1");
        channelRepo.Set(2, "1");
        channelRepo.Set(3, "1");
        await eval.UpdateCountersAsync();

        Assert.AreEqual(50, GetOutput(10));         // reset wins → StartValue
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
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init to 100

        channelRepo.Set(1, "1");
        await eval.UpdateCountersAsync();           // 101 → clamped to 100

        Assert.AreEqual(100, GetOutput(10));
    }

    [TestMethod]
    public async Task Decrement_AtMinValue_NoRoll_ClampsToMin()
    {
        var counter = BasicCounter();
        counter.StartValue = 0;
        counter.MinValue = 0;
        counter.RollAtLimit = false;
        counterRepo.Add(counter);
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init to 0

        channelRepo.Set(2, "1");
        await eval.UpdateCountersAsync();           // -1 → clamped to 0

        Assert.AreEqual(0, GetOutput(10));
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
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init to 100

        channelRepo.Set(1, "1");
        await eval.UpdateCountersAsync();           // 101 → wraps to 0

        Assert.AreEqual(0, GetOutput(10));
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
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init to 0

        channelRepo.Set(2, "1");
        await eval.UpdateCountersAsync();           // -1 → wraps to 100

        Assert.AreEqual(100, GetOutput(10));
    }

    // -------------------------------------------------------------------------
    // Unconfigured channels (ID = 0)
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task UpChannelNotConfigured_NeverIncrements()
    {
        var counter = BasicCounter();
        counter.UpChId = 0;
        counter.StartValue = 5;
        counterRepo.Add(counter);
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        Assert.AreEqual(5, GetOutput(10));
    }

    [TestMethod]
    public async Task DownChannelNotConfigured_NeverDecrements()
    {
        var counter = BasicCounter();
        counter.DownChId = 0;
        counter.StartValue = 5;
        counterRepo.Add(counter);
        channelRepo.Set(1, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();

        Assert.AreEqual(5, GetOutput(10));
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
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init to 50

        channelRepo.Set(1, "1");                    // up edge
        channelRepo.Set(2, "1");                    // down edge
        await eval.UpdateCountersAsync();           // +1 then -1 = net 0

        Assert.AreEqual(50, GetOutput(10));
    }

    // -------------------------------------------------------------------------
    // Multiple counters
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task MultipleCounters_IndependentlyEvaluated()
    {
        var c1 = new CounterDefinition { Id = 1, OutputChId = 10, UpChId = 1, DownChId = 0, ResetChId = 0, MinValue = 0, MaxValue = 100, StartValue = 0 };
        var c2 = new CounterDefinition { Id = 2, OutputChId = 20, UpChId = 0, DownChId = 2, ResetChId = 0, MinValue = 0, MaxValue = 100, StartValue = 50 };
        counterRepo.Add(c1);
        counterRepo.Add(c2);
        channelRepo.Set(1, "0");
        channelRepo.Set(2, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init: c1=0, c2=50

        channelRepo.Set(1, "1");                    // c1 up edge
        channelRepo.Set(2, "1");                    // c2 down edge
        await eval.UpdateCountersAsync();

        Assert.AreEqual(1, GetOutput(10));          // c1: 0+1
        Assert.AreEqual(49, GetOutput(20));         // c2: 50-1
    }

    // -------------------------------------------------------------------------
    // State persistence
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task EdgeTrackingState_PersistedBetweenCalls()
    {
        counterRepo.Add(BasicCounter());
        channelRepo.Set(1, "1");                    // non-zero from the start
        channelRepo.Set(2, "0");
        channelRepo.Set(3, "0");

        var eval = CreateEvaluation();
        await eval.UpdateCountersAsync();           // init, up=1 → edge fires → 1

        var state = counterRepo.GetState(1)!;
        Assert.IsFalse(state.PreviousUpWasZero);    // persisted: was non-zero
        Assert.IsTrue(state.PreviousDownWasZero);   // persisted: was zero
    }
}
