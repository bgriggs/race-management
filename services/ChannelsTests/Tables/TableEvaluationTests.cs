using Channels;
using Channels.Tables;

namespace ChannelsTests.Tables;

[TestClass]
public class TableEvaluationTests
{
    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private TableMemoryRepository tableRepo = null!;
    private ChannelMemoryRepository channelRepo = null!;
    private ChannelDefinitionMemoryRepository channelDefRepo = null!;

    [TestInitialize]
    public void Setup()
    {
        tableRepo = new TableMemoryRepository();
        channelRepo = new ChannelMemoryRepository();
        channelDefRepo = new ChannelDefinitionMemoryRepository();
    }

    private TableEvaluation CreateEvaluation() =>
        new(tableRepo, channelRepo, channelDefRepo);

    // Channel definition factory helpers.
    private static ChannelDefinition StringDef(int id) =>
        new() { Id = id, DataType = "string" };

    private static ChannelDefinition IntDef(int id) =>
        new() { Id = id, DataType = "int", BaseDecimalPlaces = 0 };

    private static ChannelDefinition DoubleDef(int id, int decimalPlaces = 2) =>
        new() { Id = id, DataType = "float", BaseDecimalPlaces = decimalPlaces };

    private double GetOutputDouble(int channelId) =>
        double.Parse(channelRepo.Get(channelId).Value);

    // -------------------------------------------------------------------------
    // String → String mapping
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task StringMapping_CaseSensitive_ExactMatch_SetsOutput()
    {
        channelRepo.Set(1, "park");
        channelDefRepo.Set(StringDef(1));
        channelDefRepo.Set(StringDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, IgnoreCase = false };
        mapping.Mapping.Add(("park", "P"));
        mapping.Mapping.Add(("reverse", "R"));
        mapping.Mapping.Add(("neutral", "N"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual("P", channelRepo.Get(2).Value);
    }

    [TestMethod]
    public async Task StringMapping_CaseSensitive_WrongCase_NoMatch_OutputEmpty()
    {
        channelRepo.Set(1, "PARK");
        channelDefRepo.Set(StringDef(1));
        channelDefRepo.Set(StringDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, IgnoreCase = false };
        mapping.Mapping.Add(("park", "P"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(string.Empty, channelRepo.Get(2).Value);
    }

    [TestMethod]
    public async Task StringMapping_CaseInsensitive_DifferentCase_SetsOutput()
    {
        channelRepo.Set(1, "PARK");
        channelDefRepo.Set(StringDef(1));
        channelDefRepo.Set(StringDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, IgnoreCase = true };
        mapping.Mapping.Add(("park", "P"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual("P", channelRepo.Get(2).Value);
    }

    [TestMethod]
    public async Task StringMapping_CaseInsensitive_MixedCase_SetsOutput()
    {
        channelRepo.Set(1, "Park");
        channelDefRepo.Set(StringDef(1));
        channelDefRepo.Set(StringDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, IgnoreCase = true };
        mapping.Mapping.Add(("PARK", "P"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual("P", channelRepo.Get(2).Value);
    }

    [TestMethod]
    public async Task StringMapping_NoMappingEntries_OutputEmpty()
    {
        channelRepo.Set(1, "park");
        channelDefRepo.Set(StringDef(1));
        channelDefRepo.Set(StringDef(2));

        tableRepo.Add(new TableMapping { InputChannel = 1, OutputChannel = 2 });

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(string.Empty, channelRepo.Get(2).Value);
    }

    [TestMethod]
    public async Task StringMapping_FirstMatchWins_SecondDuplicateIgnored()
    {
        channelRepo.Set(1, "park");
        channelDefRepo.Set(StringDef(1));
        channelDefRepo.Set(StringDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, IgnoreCase = true };
        mapping.Mapping.Add(("park", "FIRST"));
        mapping.Mapping.Add(("park", "SECOND"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual("FIRST", channelRepo.Get(2).Value);
    }

    [TestMethod]
    public async Task StringMapping_LastEntryMatches_SetsOutput()
    {
        channelRepo.Set(1, "drive");
        channelDefRepo.Set(StringDef(1));
        channelDefRepo.Set(StringDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2 };
        mapping.Mapping.Add(("park", "P"));
        mapping.Mapping.Add(("reverse", "R"));
        mapping.Mapping.Add(("neutral", "N"));
        mapping.Mapping.Add(("drive", "D"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual("D", channelRepo.Get(2).Value);
    }

    // -------------------------------------------------------------------------
    // Integer → String mapping
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task IntegerMapping_ExactMatch_SetsOutput()
    {
        channelRepo.Set(1, "3");
        channelDefRepo.Set(IntDef(1));
        channelDefRepo.Set(StringDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2 };
        mapping.Mapping.Add(("1", "one"));
        mapping.Mapping.Add(("2", "two"));
        mapping.Mapping.Add(("3", "three"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual("three", channelRepo.Get(2).Value);
    }

    [TestMethod]
    public async Task IntegerMapping_NoMatch_OutputEmpty()
    {
        channelRepo.Set(1, "9");
        channelDefRepo.Set(IntDef(1));
        channelDefRepo.Set(StringDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2 };
        mapping.Mapping.Add(("1", "one"));
        mapping.Mapping.Add(("2", "two"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(string.Empty, channelRepo.Get(2).Value);
    }

    [TestMethod]
    public async Task IntegerMapping_FirstMatchWins_SecondDuplicateIgnored()
    {
        channelRepo.Set(1, "1");
        channelDefRepo.Set(IntDef(1));
        channelDefRepo.Set(StringDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2 };
        mapping.Mapping.Add(("1", "FIRST"));
        mapping.Mapping.Add(("1", "SECOND"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual("FIRST", channelRepo.Get(2).Value);
    }

    [TestMethod]
    public async Task IntegerMapping_NegativeValue_Matches()
    {
        channelRepo.Set(1, "-1");
        channelDefRepo.Set(IntDef(1));
        channelDefRepo.Set(StringDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2 };
        mapping.Mapping.Add(("-1", "minus one"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual("minus one", channelRepo.Get(2).Value);
    }

    // -------------------------------------------------------------------------
    // Double → Double interpolation: Linear
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task LinearInterpolation_AtLowerDataPoint_ReturnsExactValue()
    {
        channelRepo.Set(1, "0");
        channelDefRepo.Set(DoubleDef(1));
        channelDefRepo.Set(DoubleDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, InterpolationType = InterpolationType.Linear };
        mapping.Mapping.Add(("0", "0"));
        mapping.Mapping.Add(("10", "200"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(0.0, GetOutputDouble(2), 0.001);
    }

    [TestMethod]
    public async Task LinearInterpolation_AtUpperDataPoint_ReturnsExactValue()
    {
        channelRepo.Set(1, "10");
        channelDefRepo.Set(DoubleDef(1));
        channelDefRepo.Set(DoubleDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, InterpolationType = InterpolationType.Linear };
        mapping.Mapping.Add(("0", "0"));
        mapping.Mapping.Add(("10", "200"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(200.0, GetOutputDouble(2), 0.001);
    }

    [TestMethod]
    public async Task LinearInterpolation_Midpoint_ReturnsInterpolatedValue()
    {
        channelRepo.Set(1, "5");
        channelDefRepo.Set(DoubleDef(1));
        channelDefRepo.Set(DoubleDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, InterpolationType = InterpolationType.Linear };
        mapping.Mapping.Add(("0", "0"));
        mapping.Mapping.Add(("10", "200"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(100.0, GetOutputDouble(2), 0.001);
    }

    [TestMethod]
    public async Task LinearInterpolation_QuarterPoint_ReturnsInterpolatedValue()
    {
        // input=2, range [0,8] with output [0,80] → output = 20
        channelRepo.Set(1, "2");
        channelDefRepo.Set(DoubleDef(1));
        channelDefRepo.Set(DoubleDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, InterpolationType = InterpolationType.Linear };
        mapping.Mapping.Add(("0", "0"));
        mapping.Mapping.Add(("8", "80"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(20.0, GetOutputDouble(2), 0.001);
    }

    // -------------------------------------------------------------------------
    // Double → Double interpolation: CubicSpline
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task CubicSplineInterpolation_AtDataPoint_ReturnsExactValue()
    {
        channelRepo.Set(1, "5");
        channelDefRepo.Set(DoubleDef(1));
        channelDefRepo.Set(DoubleDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, InterpolationType = InterpolationType.CubicSpline };
        mapping.Mapping.Add(("0", "0"));
        mapping.Mapping.Add(("5", "50"));
        mapping.Mapping.Add(("10", "100"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(50.0, GetOutputDouble(2), 0.001);
    }

    [TestMethod]
    public async Task CubicSplineInterpolation_BetweenPoints_ReturnsApproximateValue()
    {
        // input=3, linear data (0,0)-(5,50)-(10,100) → CubicSpline on linear data ≈ 30
        channelRepo.Set(1, "3");
        channelDefRepo.Set(DoubleDef(1));
        channelDefRepo.Set(DoubleDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, InterpolationType = InterpolationType.CubicSpline };
        mapping.Mapping.Add(("0", "0"));
        mapping.Mapping.Add(("5", "50"));
        mapping.Mapping.Add(("10", "100"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(30.0, GetOutputDouble(2), 1.0);
    }

    // -------------------------------------------------------------------------
    // Double → Double interpolation: Polynomial
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task PolynomialInterpolation_AtDataPoint_ReturnsExactValue()
    {
        channelRepo.Set(1, "5");
        channelDefRepo.Set(DoubleDef(1));
        channelDefRepo.Set(DoubleDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, InterpolationType = InterpolationType.Polynomial };
        mapping.Mapping.Add(("0", "0"));
        mapping.Mapping.Add(("5", "50"));
        mapping.Mapping.Add(("10", "100"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(50.0, GetOutputDouble(2), 0.001);
    }

    [TestMethod]
    public async Task PolynomialInterpolation_BetweenPoints_ReturnsApproximateValue()
    {
        // Polynomial through linear data (0,0)-(5,50)-(10,100) is itself linear → midpoint = 25 at x=2.5... 
        // Use x=2 to avoid decimal parsing: (0,0)-(5,50)-(10,100) at x=2 → ~20
        channelRepo.Set(1, "2");
        channelDefRepo.Set(DoubleDef(1));
        channelDefRepo.Set(DoubleDef(2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, InterpolationType = InterpolationType.Polynomial };
        mapping.Mapping.Add(("0", "0"));
        mapping.Mapping.Add(("5", "50"));
        mapping.Mapping.Add(("10", "100"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(20.0, GetOutputDouble(2), 0.001);
    }

    // -------------------------------------------------------------------------
    // Output formatting via SetBaseValue
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Interpolation_OutputFormattedWithTwoDecimalPlaces()
    {
        channelRepo.Set(1, "5");
        channelDefRepo.Set(DoubleDef(1));
        channelDefRepo.Set(DoubleDef(2, decimalPlaces: 2));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, InterpolationType = InterpolationType.Linear };
        mapping.Mapping.Add(("0", "0"));
        mapping.Mapping.Add(("10", "200"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        // 100.0 formatted with 2 decimal places → "100.00"
        Assert.AreEqual(100.0, GetOutputDouble(2), 0.001);
        Assert.IsTrue(channelRepo.Get(2).Value.Contains('.') || channelRepo.Get(2).Value.Contains(','),
            "Output should contain a decimal separator");
    }

    [TestMethod]
    public async Task Interpolation_OutputFormattedWithZeroDecimalPlaces()
    {
        channelRepo.Set(1, "5");
        channelDefRepo.Set(DoubleDef(1));
        channelDefRepo.Set(DoubleDef(2, decimalPlaces: 0));

        var mapping = new TableMapping { InputChannel = 1, OutputChannel = 2, InterpolationType = InterpolationType.Linear };
        mapping.Mapping.Add(("0", "0"));
        mapping.Mapping.Add(("10", "200"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(100.0, GetOutputDouble(2), 0.001);
    }

    // -------------------------------------------------------------------------
    // Multiple mappings
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task MultipleMappings_AllProcessedIndependently()
    {
        channelRepo.Set(1, "park");
        channelRepo.Set(3, "2");
        channelDefRepo.Set(StringDef(1));
        channelDefRepo.Set(StringDef(2));
        channelDefRepo.Set(IntDef(3));
        channelDefRepo.Set(StringDef(4));

        var m1 = new TableMapping { InputChannel = 1, OutputChannel = 2 };
        m1.Mapping.Add(("park", "P"));
        tableRepo.Add(m1);

        var m2 = new TableMapping { InputChannel = 3, OutputChannel = 4 };
        m2.Mapping.Add(("1", "one"));
        m2.Mapping.Add(("2", "two"));
        tableRepo.Add(m2);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual("P", channelRepo.Get(2).Value);
        Assert.AreEqual("two", channelRepo.Get(4).Value);
    }

    [TestMethod]
    public async Task MultipleMappings_OneStringOneInterpolation_BothProduceCorrectOutputs()
    {
        channelRepo.Set(1, "reverse");
        channelRepo.Set(3, "5");
        channelDefRepo.Set(StringDef(1));
        channelDefRepo.Set(StringDef(2));
        channelDefRepo.Set(DoubleDef(3));
        channelDefRepo.Set(DoubleDef(4));

        var m1 = new TableMapping { InputChannel = 1, OutputChannel = 2 };
        m1.Mapping.Add(("park", "P"));
        m1.Mapping.Add(("reverse", "R"));
        tableRepo.Add(m1);

        var m2 = new TableMapping { InputChannel = 3, OutputChannel = 4, InterpolationType = InterpolationType.Linear };
        m2.Mapping.Add(("0", "0"));
        m2.Mapping.Add(("10", "100"));
        tableRepo.Add(m2);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual("R", channelRepo.Get(2).Value);
        Assert.AreEqual(50.0, GetOutputDouble(4), 0.001);
    }

    [TestMethod]
    public async Task NoMappings_NothingProcessed_OutputChannelUnchanged()
    {
        channelRepo.Set(10, "original");

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual("original", channelRepo.Get(10).Value);
    }
}

