using Channels;
using Channels.Math;

namespace ChannelsTests.Math;

[TestClass]
public class MathEvaluationTests
{
    private static readonly Guid Ch1 = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Ch2 = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid ChOut = new("00000000-0000-0000-0000-000000000010");
    private static readonly Guid ChOut2 = new("00000000-0000-0000-0000-000000000014");
    private static readonly Guid ChInt = new("00000000-0000-0000-0000-000000000063");
    private static readonly Guid ChOut3 = new("00000000-0000-0000-0000-000000000064");

    private static Guid MathId(int id) => new($"00000000-0000-0000-0002-{id:000000000000}");

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private MathMemoryRepository mathRepo = null!;
    private ChannelMemoryRepository channelRepo = null!;
    private ChannelDefinitionMemoryRepository channelDefRepo = null!;

    [TestInitialize]
    public void Setup()
    {
        mathRepo = new MathMemoryRepository();
        channelRepo = new ChannelMemoryRepository();
        channelDefRepo = new ChannelDefinitionMemoryRepository();
    }

    private MathEvaluation CreateEvaluation() =>
        new(mathRepo, channelRepo, channelDefRepo);

    private void SetupInputChannel(Guid id, string value, string baseUnit = "km") =>
        SetupChannel(id, value, baseUnit, 2);

    private void SetupChannel(Guid id, string value, string baseUnit, int decimalPlaces)
    {
        channelRepo.Set(id, value);
        channelDefRepo.Set(new ChannelDefinition { Id = id, BaseUnitType = baseUnit, BaseDecimalPlaces = decimalPlaces });
    }

    private void SetupOutputChannel(Guid id, int decimalPlaces = 2) =>
        channelDefRepo.Set(new ChannelDefinition { Id = id, BaseDecimalPlaces = decimalPlaces });

    private double GetOutput(Guid channelId) =>
        double.Parse(channelRepo.Get(channelId).Value);

    // -------------------------------------------------------------------------
    // Bias: output = ch1 / (ch1 + ch2)
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Bias_TypicalValues_ReturnsChannel1DividedBySum()
    {
        SetupInputChannel(Ch1, "3");
        SetupInputChannel(Ch2, "1");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.Bias, Channel1Id = Ch1, Channel2Id = Ch2, OutputChannelId = ChOut });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(0.75, GetOutput(ChOut), 0.001);   // 3 / (3 + 1)
    }

    [TestMethod]
    public async Task Bias_EqualChannelValues_ReturnsHalf()
    {
        SetupInputChannel(Ch1, "5");
        SetupInputChannel(Ch2, "5");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.Bias, Channel1Id = Ch1, Channel2Id = Ch2, OutputChannelId = ChOut });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(0.5, GetOutput(ChOut), 0.001);    // 5 / (5 + 5)
    }

    [TestMethod]
    public async Task Bias_Channel1Dominant_ReturnsValueCloseToOne()
    {
        SetupInputChannel(Ch1, "9");
        SetupInputChannel(Ch2, "1");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.Bias, Channel1Id = Ch1, Channel2Id = Ch2, OutputChannelId = ChOut });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(0.9, GetOutput(ChOut), 0.001);    // 9 / (9 + 1)
    }

    // -------------------------------------------------------------------------
    // LinearCorrector: output = (ch1 * A) + B
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task LinearCorrector_PositiveScaleAndOffset_ReturnsCorrectValue()
    {
        SetupInputChannel(Ch1, "5");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.LinearCorrector, Channel1Id = Ch1, OutputChannelId = ChOut, A = 2m, B = 3m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(13.0, GetOutput(ChOut), 0.001);   // (5 * 2) + 3
    }

    [TestMethod]
    public async Task LinearCorrector_ZeroOffset_ReturnsScaledValue()
    {
        SetupInputChannel(Ch1, "4");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.LinearCorrector, Channel1Id = Ch1, OutputChannelId = ChOut, A = 3m, B = 0m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(12.0, GetOutput(ChOut), 0.001);   // (4 * 3) + 0
    }

    [TestMethod]
    public async Task LinearCorrector_ZeroScale_ReturnsOffset()
    {
        SetupInputChannel(Ch1, "100");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.LinearCorrector, Channel1Id = Ch1, OutputChannelId = ChOut, A = 0m, B = 7m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(7.0, GetOutput(ChOut), 0.001);    // (100 * 0) + 7
    }

    [TestMethod]
    public async Task LinearCorrector_NegativeOffset_SubtractsFromScaled()
    {
        SetupInputChannel(Ch1, "10");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.LinearCorrector, Channel1Id = Ch1, OutputChannelId = ChOut, A = 2m, B = -5m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(15.0, GetOutput(ChOut), 0.001);   // (10 * 2) + (-5)
    }

    // -------------------------------------------------------------------------
    // SimpleOperation: output = ch1 OP A  (or ch1 OP ch2 when Channel2Id != Guid.Empty)
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task SimpleOperation_Add_WithConstantA_AddsA()
    {
        SetupInputChannel(Ch1, "5");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Add, Channel1Id = Ch1, Channel2Id = Guid.Empty, OutputChannelId = ChOut, A = 3m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(8.0, GetOutput(ChOut), 0.001);    // 5 + 3
    }

    [TestMethod]
    public async Task SimpleOperation_Add_WithChannel2_AddsChannel2Value()
    {
        SetupInputChannel(Ch1, "5");
        SetupInputChannel(Ch2, "7");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Add, Channel1Id = Ch1, Channel2Id = Ch2, OutputChannelId = ChOut });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(12.0, GetOutput(ChOut), 0.001);   // 5 + 7
    }

    [TestMethod]
    public async Task SimpleOperation_Subtract_WithConstantA_SubtractsA()
    {
        SetupInputChannel(Ch1, "10");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Subtract, Channel1Id = Ch1, Channel2Id = Guid.Empty, OutputChannelId = ChOut, A = 4m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(6.0, GetOutput(ChOut), 0.001);    // 10 - 4
    }

    [TestMethod]
    public async Task SimpleOperation_Subtract_WithChannel2_SubtractsChannel2Value()
    {
        SetupInputChannel(Ch1, "10");
        SetupInputChannel(Ch2, "3");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Subtract, Channel1Id = Ch1, Channel2Id = Ch2, OutputChannelId = ChOut });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(7.0, GetOutput(ChOut), 0.001);    // 10 - 3
    }

    [TestMethod]
    public async Task SimpleOperation_Multiply_WithConstantA_MultipliesA()
    {
        SetupInputChannel(Ch1, "3");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Multiply, Channel1Id = Ch1, Channel2Id = Guid.Empty, OutputChannelId = ChOut, A = 4m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(12.0, GetOutput(ChOut), 0.001);   // 3 * 4
    }

    [TestMethod]
    public async Task SimpleOperation_Divide_WithConstantA_DividesA()
    {
        SetupInputChannel(Ch1, "10");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Divide, Channel1Id = Ch1, Channel2Id = Guid.Empty, OutputChannelId = ChOut, A = 4m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(2.5, GetOutput(ChOut), 0.001);    // 10 / 4
    }

    [TestMethod]
    public async Task SimpleOperation_Divide_WithChannel2_DividesChannel2Value()
    {
        SetupInputChannel(Ch1, "12");
        SetupInputChannel(Ch2, "4");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Divide, Channel1Id = Ch1, Channel2Id = Ch2, OutputChannelId = ChOut });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(3.0, GetOutput(ChOut), 0.001);    // 12 / 4
    }

    // -------------------------------------------------------------------------
    // DivisionInteger: output = Truncate(ch1 / A)
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DivisionInteger_WithRemainder_TruncatesDecimalPart()
    {
        SetupInputChannel(Ch1, "7");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.DivisionInteger, Channel1Id = Ch1, OutputChannelId = ChOut, A = 2m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(3.0, GetOutput(ChOut), 0.001);    // Truncate(7 / 2) = Truncate(3.5) = 3
    }

    [TestMethod]
    public async Task DivisionInteger_ExactDivision_ReturnsWholeNumber()
    {
        SetupInputChannel(Ch1, "9");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.DivisionInteger, Channel1Id = Ch1, OutputChannelId = ChOut, A = 3m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(3.0, GetOutput(ChOut), 0.001);    // Truncate(9 / 3) = 3
    }

    [TestMethod]
    public async Task DivisionInteger_NegativeResult_TruncatesTowardZero()
    {
        SetupInputChannel(Ch1, "7");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.DivisionInteger, Channel1Id = Ch1, OutputChannelId = ChOut, A = -2m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(-3.0, GetOutput(ChOut), 0.001);   // Truncate(7 / -2) = Truncate(-3.5) = -3
    }

    // -------------------------------------------------------------------------
    // DivisionModulo: output = Truncate(ch1 % A)
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task DivisionModulo_WithRemainder_ReturnsTruncatedRemainder()
    {
        SetupInputChannel(Ch1, "7");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.DivisionModulo, Channel1Id = Ch1, OutputChannelId = ChOut, A = 3m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(1.0, GetOutput(ChOut), 0.001);    // Truncate(7 % 3) = Truncate(1) = 1
    }

    [TestMethod]
    public async Task DivisionModulo_ExactDivision_ReturnsZero()
    {
        SetupInputChannel(Ch1, "9");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.DivisionModulo, Channel1Id = Ch1, OutputChannelId = ChOut, A = 3m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(0.0, GetOutput(ChOut), 0.001);    // Truncate(9 % 3) = 0
    }

    [TestMethod]
    public async Task DivisionModulo_LargerDivisor_ReturnsOriginalValue()
    {
        SetupInputChannel(Ch1, "4");
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.DivisionModulo, Channel1Id = Ch1, OutputChannelId = ChOut, A = 10m });

        await CreateEvaluation().RunCalculationsAsync();

        Assert.AreEqual(4.0, GetOutput(ChOut), 0.001);    // Truncate(4 % 10) = 4
    }

    // -------------------------------------------------------------------------
    // Output formatting
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Output_FormattedWithDefinedDecimalPlaces()
    {
        SetupInputChannel(Ch1, "5");
        channelDefRepo.Set(new ChannelDefinition { Id = ChOut, BaseDecimalPlaces = 3 });

        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.LinearCorrector, Channel1Id = Ch1, OutputChannelId = ChOut, A = 2m, B = 0m });

        await CreateEvaluation().RunCalculationsAsync();

        var raw = channelRepo.Get(ChOut).Value;
        Assert.AreEqual(10.0, double.Parse(raw), 0.0001);
        Assert.IsTrue(raw.Contains('.') || raw.Contains(','), "Should contain a decimal separator");
    }

    // -------------------------------------------------------------------------
    // Exception paths: channel missing BaseUnitType → GetOutputQuantity returns null
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Channel1WithoutBaseUnit_ThrowsInvalidOperationException()
    {
        channelRepo.Set(Ch1, "5");
        channelDefRepo.Set(new ChannelDefinition { Id = Ch1, BaseUnitType = string.Empty });
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.LinearCorrector, Channel1Id = Ch1, OutputChannelId = ChOut, A = 1m, B = 0m });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateEvaluation().RunCalculationsAsync());
    }

    [TestMethod]
    public async Task BiasChannel2WithoutBaseUnit_ThrowsInvalidOperationException()
    {
        SetupInputChannel(Ch1, "3");
        channelRepo.Set(Ch2, "1");
        channelDefRepo.Set(new ChannelDefinition { Id = Ch2, BaseUnitType = string.Empty });
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.Bias, Channel1Id = Ch1, Channel2Id = Ch2, OutputChannelId = ChOut });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateEvaluation().RunCalculationsAsync());
    }

    [TestMethod]
    public async Task SimpleOperationChannel2WithoutBaseUnit_ThrowsInvalidOperationException()
    {
        SetupInputChannel(Ch1, "5");
        channelRepo.Set(Ch2, "3");
        channelDefRepo.Set(new ChannelDefinition { Id = Ch2, BaseUnitType = string.Empty });
        SetupOutputChannel(ChOut);
        mathRepo.Add(new MathDefinition { Id = MathId(1), Name = "math", Type = MathType.SimpleOperation, SimpleOperationType = SimpleOperationType.Add, Channel1Id = Ch1, Channel2Id = Ch2, OutputChannelId = ChOut });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateEvaluation().RunCalculationsAsync());
    }
}
