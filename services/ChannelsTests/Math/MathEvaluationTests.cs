using Channels;
using Channels.Math;

namespace ChannelsTests.Math;

[TestClass]
public class MathEvaluationTests
{
    // -------------------------------------------------------------------------
    // Fakes
    // -------------------------------------------------------------------------

    private sealed class FakeMathRepository : IMathRepository
    {
        private readonly List<MathDefinition> definitions = [];

        public void Add(MathDefinition definition) => definitions.Add(definition);

        public Task<IEnumerable<MathDefinition>> GetDefinitionsAsync() =>
            Task.FromResult(definitions.AsEnumerable());
    }

    private sealed class FakeChannelRepository : IChannelRepository
    {
        private readonly Dictionary<int, ChannelValue> channels = [];

        public void Set(int id, string value) =>
            channels[id] = new ChannelValue { Id = id, Value = value };

        public ChannelValue Get(int id) =>
            channels.TryGetValue(id, out var v) ? v : new ChannelValue { Id = id };

        public Task<ChannelValue> GetChannelValueAsync(int channelId) =>
            Task.FromResult(channels.TryGetValue(channelId, out var v) ? v : new ChannelValue { Id = channelId });

        public Task SetChannelValueAsync(ChannelValue ch)
        {
            channels[ch.Id] = ch;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeChannelDefinitionRepository : IChannelDefinitionRepository
    {
        private readonly Dictionary<int, ChannelDefinition> defs = [];

        public void Set(ChannelDefinition def) => defs[def.Id] = def;

        public Task<ChannelDefinition> GetChannelDefinitionAsync(int channelId) =>
            Task.FromResult(defs.TryGetValue(channelId, out var d) ? d : new ChannelDefinition { Id = channelId });
    }

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private FakeMathRepository mathRepo = null!;
    private FakeChannelRepository channelRepo = null!;
    private FakeChannelDefinitionRepository channelDefRepo = null!;

    [TestInitialize]
    public void Setup()
    {
        mathRepo = new FakeMathRepository();
        channelRepo = new FakeChannelRepository();
        channelDefRepo = new FakeChannelDefinitionRepository();
    }

    private MathEvaluation CreateEvaluation() =>
        new(mathRepo, channelRepo, channelDefRepo);

    /// <summary>
    /// Sets up a channel with a value and an unambiguous base unit so GetOutputQuantity
    /// returns a non-null IQuantity. "m" is ambiguous in UnitsNet v6 (Meter vs Minute),
    /// so "km" is used as the default.
    /// </summary>
    private void SetupInputChannel(int id, string value, string baseUnit = "km") =>
        SetupChannel(id, value, baseUnit, 2);

    /// <summary>
    /// Sets up a channel that can serve as both an intermediate output and a subsequent input.
    /// Requires both BaseDecimalPlaces (for SetBaseValue formatting) and BaseUnitType
    /// (so GetOutputQuantity can construct an IQuantity when the channel is read back).
    /// </summary>
    private void SetupChannel(int id, string value, string baseUnit, int decimalPlaces)
    {
        channelRepo.Set(id, value);
        channelDefRepo.Set(new ChannelDefinition { Id = id, BaseUnitType = baseUnit, BaseDecimalPlaces = decimalPlaces });
    }

    private void SetupOutputChannel(int id, int decimalPlaces = 2) =>
        channelDefRepo.Set(new ChannelDefinition { Id = id, BaseDecimalPlaces = decimalPlaces });

    private double GetOutput(int channelId) =>
        double.Parse(channelRepo.Get(channelId).Value);

    // -------------------------------------------------------------------------
    // Bias: output = ch1 / (ch1 + ch2)
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Bias_TypicalValues_ReturnsChannel1DividedBySum()
    {
        SetupInputChannel(1, "3");
        SetupInputChannel(2, "1");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.Bias, Channel1Id = 1, Channel2Id = 2, OutputChannelId = 10 });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(0.75, GetOutput(10), 0.001);   // 3 / (3 + 1)
    }

    [TestMethod]
    public async Task Bias_EqualChannelValues_ReturnsHalf()
    {
        SetupInputChannel(1, "5");
        SetupInputChannel(2, "5");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.Bias, Channel1Id = 1, Channel2Id = 2, OutputChannelId = 10 });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(0.5, GetOutput(10), 0.001);    // 5 / (5 + 5)
    }

    [TestMethod]
    public async Task Bias_Channel1Dominant_ReturnsValueCloseToOne()
    {
        SetupInputChannel(1, "9");
        SetupInputChannel(2, "1");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.Bias, Channel1Id = 1, Channel2Id = 2, OutputChannelId = 10 });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(0.9, GetOutput(10), 0.001);    // 9 / (9 + 1)
    }

    // -------------------------------------------------------------------------
    // LinearCorrector: output = (ch1 * A) + B
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task LinearCorrector_PositiveScaleAndOffset_ReturnsCorrectValue()
    {
        SetupInputChannel(1, "5");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.LinearCorrector, Channel1Id = 1, OutputChannelId = 10, A = 2m, B = 3m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(13.0, GetOutput(10), 0.001);   // (5 * 2) + 3
    }

    [TestMethod]
    public async Task LinearCorrector_ZeroOffset_ReturnsScaledValue()
    {
        SetupInputChannel(1, "4");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.LinearCorrector, Channel1Id = 1, OutputChannelId = 10, A = 3m, B = 0m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(12.0, GetOutput(10), 0.001);   // (4 * 3) + 0
    }

    [TestMethod]
    public async Task LinearCorrector_ZeroScale_ReturnsOffset()
    {
        SetupInputChannel(1, "100");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.LinearCorrector, Channel1Id = 1, OutputChannelId = 10, A = 0m, B = 7m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(7.0, GetOutput(10), 0.001);    // (100 * 0) + 7
    }

    [TestMethod]
    public async Task LinearCorrector_NegativeOffset_SubtractsFromScaled()
    {
        SetupInputChannel(1, "10");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.LinearCorrector, Channel1Id = 1, OutputChannelId = 10, A = 2m, B = -5m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(15.0, GetOutput(10), 0.001);   // (10 * 2) + (-5)
    }

    // -------------------------------------------------------------------------
    // SimpleOperation: output = ch1 OP A  (or ch1 OP ch2 when Channel2Id > 0)
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task SimpleOperation_Add_WithConstantA_AddsA()
    {
        SetupInputChannel(1, "5");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Add, Channel1Id = 1, Channel2Id = 0, OutputChannelId = 10, A = 3m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(8.0, GetOutput(10), 0.001);    // 5 + 3
    }

    [TestMethod]
    public async Task SimpleOperation_Add_WithChannel2_AddsChannel2Value()
    {
        SetupInputChannel(1, "5");
        SetupInputChannel(2, "7");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Add, Channel1Id = 1, Channel2Id = 2, OutputChannelId = 10 });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(12.0, GetOutput(10), 0.001);   // 5 + 7
    }

    [TestMethod]
    public async Task SimpleOperation_Subtract_WithConstantA_SubtractsA()
    {
        SetupInputChannel(1, "10");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Subtract, Channel1Id = 1, Channel2Id = 0, OutputChannelId = 10, A = 4m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(6.0, GetOutput(10), 0.001);    // 10 - 4
    }

    [TestMethod]
    public async Task SimpleOperation_Subtract_WithChannel2_SubtractsChannel2Value()
    {
        SetupInputChannel(1, "10");
        SetupInputChannel(2, "3");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Subtract, Channel1Id = 1, Channel2Id = 2, OutputChannelId = 10 });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(7.0, GetOutput(10), 0.001);    // 10 - 3
    }

    [TestMethod]
    public async Task SimpleOperation_Multiply_WithConstantA_MultipliesA()
    {
        SetupInputChannel(1, "3");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Multiply, Channel1Id = 1, Channel2Id = 0, OutputChannelId = 10, A = 4m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(12.0, GetOutput(10), 0.001);   // 3 * 4
    }

    [TestMethod]
    public async Task SimpleOperation_Divide_WithConstantA_DividesA()
    {
        SetupInputChannel(1, "10");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Divide, Channel1Id = 1, Channel2Id = 0, OutputChannelId = 10, A = 4m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(2.5, GetOutput(10), 0.001);    // 10 / 4
    }

    [TestMethod]
    public async Task SimpleOperation_Divide_WithChannel2_DividesChannel2Value()
    {
        SetupInputChannel(1, "12");
        SetupInputChannel(2, "4");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Divide, Channel1Id = 1, Channel2Id = 2, OutputChannelId = 10 });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(3.0, GetOutput(10), 0.001);    // 12 / 4
    }

    // -------------------------------------------------------------------------
    // DivisionInteger: output = Truncate(ch1 / A)
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DivisionInteger_WithRemainder_TruncatesDecimalPart()
    {
        SetupInputChannel(1, "7");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.DivisionInteger, Channel1Id = 1, OutputChannelId = 10, A = 2m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(3.0, GetOutput(10), 0.001);    // Truncate(7 / 2) = Truncate(3.5) = 3
    }

    [TestMethod]
    public async Task DivisionInteger_ExactDivision_ReturnsWholeNumber()
    {
        SetupInputChannel(1, "9");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.DivisionInteger, Channel1Id = 1, OutputChannelId = 10, A = 3m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(3.0, GetOutput(10), 0.001);    // Truncate(9 / 3) = 3
    }

    [TestMethod]
    public async Task DivisionInteger_NegativeResult_TruncatesTowardZero()
    {
        SetupInputChannel(1, "7");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.DivisionInteger, Channel1Id = 1, OutputChannelId = 10, A = -2m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(-3.0, GetOutput(10), 0.001);   // Truncate(7 / -2) = Truncate(-3.5) = -3
    }

    // -------------------------------------------------------------------------
    // DivisionModulo: output = Truncate(ch1 % A)
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DivisionModulo_WithRemainder_ReturnsTruncatedRemainder()
    {
        SetupInputChannel(1, "7");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.DivisionModulo, Channel1Id = 1, OutputChannelId = 10, A = 3m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(1.0, GetOutput(10), 0.001);    // Truncate(7 % 3) = Truncate(1) = 1
    }

    [TestMethod]
    public async Task DivisionModulo_ExactDivision_ReturnsZero()
    {
        SetupInputChannel(1, "9");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.DivisionModulo, Channel1Id = 1, OutputChannelId = 10, A = 3m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(0.0, GetOutput(10), 0.001);    // Truncate(9 % 3) = 0
    }

    [TestMethod]
    public async Task DivisionModulo_LargerDivisor_ReturnsOriginalValue()
    {
        SetupInputChannel(1, "4");
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.DivisionModulo, Channel1Id = 1, OutputChannelId = 10, A = 10m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(4.0, GetOutput(10), 0.001);    // Truncate(4 % 10) = 4
    }

    // -------------------------------------------------------------------------
    // Ordering: operations execute in ascending Order value
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Order_MultipleParameters_ExecutedInAscendingOrder()
    {
        // Op with Order=2 is added first — it must still run after Order=1.
        SetupInputChannel(1, "5");
        SetupOutputChannel(10);
        SetupOutputChannel(20);

        mathRepo.Add(new MathDefinition { Id = 2, Order = 2, Type = MathType.LinearCorrector, Channel1Id = 1, OutputChannelId = 20, A = 3m, B = 0m });
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.LinearCorrector, Channel1Id = 1, OutputChannelId = 10, A = 2m, B = 0m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(10.0, GetOutput(10), 0.001);   // 5 * 2
        Assert.AreEqual(15.0, GetOutput(20), 0.001);   // 5 * 3
    }

    [TestMethod]
    public async Task Order_LaterOperationReadsOutputOfEarlierOperation()
    {
        // Op1 (Order=1): ch1=5, Add A=10 → writes 15 to ch99.
        // Op2 (Order=2): reads ch99 (now 15), Multiply A=2 → writes 30 to ch100.
        // ch99 must have BaseUnitType so GetOutputQuantity returns non-null when Op2 reads it.
        SetupInputChannel(1, "5");
        SetupChannel(99, "0", baseUnit: "km", decimalPlaces: 2);   // intermediate
        SetupOutputChannel(100);

        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Add, Channel1Id = 1, Channel2Id = 0, OutputChannelId = 99, A = 10m });
        mathRepo.Add(new MathDefinition { Id = 2, Order = 2, Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Multiply, Channel1Id = 99, Channel2Id = 0, OutputChannelId = 100, A = 2m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(15.0, GetOutput(99), 0.001);   // 5 + 10
        Assert.AreEqual(30.0, GetOutput(100), 0.001);  // 15 * 2
    }

    // -------------------------------------------------------------------------
    // Output formatting
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Output_FormattedWithDefinedDecimalPlaces()
    {
        SetupInputChannel(1, "5");
        channelDefRepo.Set(new ChannelDefinition { Id = 10, BaseDecimalPlaces = 3 });

        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.LinearCorrector, Channel1Id = 1, OutputChannelId = 10, A = 2m, B = 0m });

        await CreateEvaluation().RunCalculationsAsync();

        // 5 * 2 = 10, formatted to 3 decimal places → "10.000"
        var raw = channelRepo.Get(10).Value;
        Assert.AreEqual(10.0, double.Parse(raw), 0.0001);
        Assert.IsTrue(raw.Contains('.') || raw.Contains(','), "Should contain a decimal separator");
    }

    // -------------------------------------------------------------------------
    // Exception paths: channel missing BaseUnitType → GetOutputQuantity returns null
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Channel1WithoutBaseUnit_ThrowsInvalidOperationException()
    {
        // Channel has a value but no BaseUnitType → GetOutputQuantity returns null.
        channelRepo.Set(1, "5");
        channelDefRepo.Set(new ChannelDefinition { Id = 1, BaseUnitType = string.Empty });
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.LinearCorrector, Channel1Id = 1, OutputChannelId = 10, A = 1m, B = 0m });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateEvaluation().RunCalculationsAsync());
    }

    [TestMethod]
    public async Task BiasChannel2WithoutBaseUnit_ThrowsInvalidOperationException()
    {
        SetupInputChannel(1, "3");
        channelRepo.Set(2, "1");
        channelDefRepo.Set(new ChannelDefinition { Id = 2, BaseUnitType = string.Empty });
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.Bias, Channel1Id = 1, Channel2Id = 2, OutputChannelId = 10 });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateEvaluation().RunCalculationsAsync());
    }

    [TestMethod]
    public async Task SimpleOperationChannel2WithoutBaseUnit_ThrowsInvalidOperationException()
    {
        SetupInputChannel(1, "5");
        channelRepo.Set(2, "3");
        channelDefRepo.Set(new ChannelDefinition { Id = 2, BaseUnitType = string.Empty });
        SetupOutputChannel(10);
        mathRepo.Add(new MathDefinition { Id = 1, Order = 1, Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Add, Channel1Id = 1, Channel2Id = 2, OutputChannelId = 10 });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateEvaluation().RunCalculationsAsync());
    }
}

