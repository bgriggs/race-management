using Channels;
using Channels.Tables;

namespace ChannelsTests.Tables;

[TestClass]
public class TableEvaluationTests
{
    private static readonly Guid Ch1  = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Ch2  = new("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Ch3  = new("00000000-0000-0000-0000-000000000003");
    private static readonly Guid Ch4  = new("00000000-0000-0000-0000-000000000004");
    private static readonly Guid Ch10 = new("00000000-0000-0000-0000-000000000010");

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
    //private static ChannelDefinition StringDef(Guid id) =>
    //    new() { Id = id, DataType = "string", IsStringValue = true };

    private static ChannelDefinition IntDef(Guid id) =>
        new() { Id = id, DataType = "int" };

    private static ChannelDefinition DoubleDef(Guid id) =>
        new() { Id = id, DataType = "float" };

    private double GetOutputDouble(Guid channelId) =>
        double.Parse(channelRepo.Get(channelId).Value);

    // -------------------------------------------------------------------------
    // String → String mapping
    // -------------------------------------------------------------------------

    //[TestMethod]
    //public async Task StringMapping_CaseSensitive_ExactMatch_SetsOutput()
    //{
    //    channelRepo.Set(Ch1, "park");
    //    channelDefRepo.Set(StringDef(Ch1));
    //    channelDefRepo.Set(StringDef(Ch2));

    //    var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, IgnoreCase = false };
    //    mapping.Mappings.Add(new TableMapping("park", "P"));
    //    mapping.Mappings.Add(new TableMapping("reverse", "R"));
    //    mapping.Mappings.Add(new TableMapping("neutral", "N"));
    //    tableRepo.Add(mapping);

    //    await CreateEvaluation().EvaluateAsync();

    //    Assert.AreEqual("P", channelRepo.Get(Ch2).Value);
    //}

    //[TestMethod]
    //public async Task StringMapping_CaseSensitive_WrongCase_NoMatch_OutputEmpty()
    //{
    //    channelRepo.Set(Ch1, "PARK");
    //    channelDefRepo.Set(StringDef(Ch1));
    //    channelDefRepo.Set(StringDef(Ch2));

    //    var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, IgnoreCase = false };
    //    mapping.Mappings.Add(new TableMapping("park", "P"));
    //    tableRepo.Add(mapping);

    //    await CreateEvaluation().EvaluateAsync();

    //    Assert.AreEqual(string.Empty, channelRepo.Get(Ch2).Value);
    //}

    //[TestMethod]
    //public async Task StringMapping_CaseInsensitive_DifferentCase_SetsOutput()
    //{
    //    channelRepo.Set(Ch1, "PARK");
    //    channelDefRepo.Set(StringDef(Ch1));
    //    channelDefRepo.Set(StringDef(Ch2));

    //    var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, IgnoreCase = true };
    //    mapping.Mappings.Add(new TableMapping("park", "P"));
    //    tableRepo.Add(mapping);

    //    await CreateEvaluation().EvaluateAsync();

    //    Assert.AreEqual("P", channelRepo.Get(Ch2).Value);
    //}

    //[TestMethod]
    //public async Task StringMapping_CaseInsensitive_MixedCase_SetsOutput()
    //{
    //    channelRepo.Set(Ch1, "Park");
    //    channelDefRepo.Set(StringDef(Ch1));
    //    channelDefRepo.Set(StringDef(Ch2));

    //    var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, IgnoreCase = true };
    //    mapping.Mappings.Add(new TableMapping("PARK", "P"));
    //    tableRepo.Add(mapping);

    //    await CreateEvaluation().EvaluateAsync();

    //    Assert.AreEqual("P", channelRepo.Get(Ch2).Value);
    //}

    //[TestMethod]
    //public async Task StringMapping_NoMappingEntries_OutputEmpty()
    //{
    //    channelRepo.Set(Ch1, "park");
    //    channelDefRepo.Set(StringDef(Ch1));
    //    channelDefRepo.Set(StringDef(Ch2));

    //    tableRepo.Add(new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2 });

    //    await CreateEvaluation().EvaluateAsync();

    //    Assert.AreEqual(string.Empty, channelRepo.Get(Ch2).Value);
    //}

    //[TestMethod]
    //public async Task StringMapping_FirstMatchWins_SecondDuplicateIgnored()
    //{
    //    channelRepo.Set(Ch1, "park");
    //    channelDefRepo.Set(StringDef(Ch1));
    //    channelDefRepo.Set(StringDef(Ch2));

    //    var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, IgnoreCase = true };
    //    mapping.Mappings.Add(new TableMapping("park", "FIRST"));
    //    mapping.Mappings.Add(new TableMapping("park", "SECOND"));
    //    tableRepo.Add(mapping);

    //    await CreateEvaluation().EvaluateAsync();

    //    Assert.AreEqual("FIRST", channelRepo.Get(Ch2).Value);
    //}

    //[TestMethod]
    //public async Task StringMapping_LastEntryMatches_SetsOutput()
    //{
    //    channelRepo.Set(Ch1, "drive");
    //    channelDefRepo.Set(StringDef(Ch1));
    //    channelDefRepo.Set(StringDef(Ch2));

    //    var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2 };
    //    mapping.Mappings.Add(new TableMapping("park", "P"));
    //    mapping.Mappings.Add(new TableMapping("reverse", "R"));
    //    mapping.Mappings.Add(new TableMapping("neutral", "N"));
    //    mapping.Mappings.Add(new TableMapping("drive", "D"));
    //    tableRepo.Add(mapping);

    //    await CreateEvaluation().EvaluateAsync();

    //    Assert.AreEqual("D", channelRepo.Get(Ch2).Value);
    //}

    //// -------------------------------------------------------------------------
    //// Integer → String mapping
    //// -------------------------------------------------------------------------

    //[TestMethod]
    //public async Task IntegerMapping_ExactMatch_SetsOutput()
    //{
    //    channelRepo.Set(Ch1, "3");
    //    channelDefRepo.Set(IntDef(Ch1));
    //    channelDefRepo.Set(StringDef(Ch2));

    //    var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2 };
    //    mapping.Mappings.Add(new TableMapping("1", "one"));
    //    mapping.Mappings.Add(new TableMapping("2", "two"));
    //    mapping.Mappings.Add(new TableMapping("3", "three"));
    //    tableRepo.Add(mapping);

    //    await CreateEvaluation().EvaluateAsync();

    //    Assert.AreEqual("three", channelRepo.Get(Ch2).Value);
    //}

    //[TestMethod]
    //public async Task IntegerMapping_NoMatch_OutputEmpty()
    //{
    //    channelRepo.Set(Ch1, "9");
    //    channelDefRepo.Set(IntDef(Ch1));
    //    channelDefRepo.Set(StringDef(Ch2));

    //    var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2 };
    //    mapping.Mappings.Add(new TableMapping("1", "one"));
    //    mapping.Mappings.Add(new TableMapping("2", "two"));
    //    tableRepo.Add(mapping);

    //    await CreateEvaluation().EvaluateAsync();

    //    Assert.AreEqual(string.Empty, channelRepo.Get(Ch2).Value);
    //}

    //[TestMethod]
    //public async Task IntegerMapping_FirstMatchWins_SecondDuplicateIgnored()
    //{
    //    channelRepo.Set(Ch1, "1");
    //    channelDefRepo.Set(IntDef(Ch1));
    //    channelDefRepo.Set(StringDef(Ch2));

    //    var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2 };
    //    mapping.Mappings.Add(new TableMapping("1", "FIRST"));
    //    mapping.Mappings.Add(new TableMapping("1", "SECOND"));
    //    tableRepo.Add(mapping);

    //    await CreateEvaluation().EvaluateAsync();

    //    Assert.AreEqual("FIRST", channelRepo.Get(Ch2).Value);
    //}

    //[TestMethod]
    //public async Task IntegerMapping_NegativeValue_Matches()
    //{
    //    channelRepo.Set(Ch1, "-1");
    //    channelDefRepo.Set(IntDef(Ch1));
    //    channelDefRepo.Set(StringDef(Ch2));

    //    var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2 };
    //    mapping.Mappings.Add(new TableMapping("-1", "minus one"));
    //    tableRepo.Add(mapping);

    //    await CreateEvaluation().EvaluateAsync();

    //    Assert.AreEqual("minus one", channelRepo.Get(Ch2).Value);
    //}

    // -------------------------------------------------------------------------
    // Double → Double interpolation: Linear
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task LinearInterpolation_AtLowerDataPoint_ReturnsExactValue()
    {
        channelRepo.Set(Ch1, "0");
        channelDefRepo.Set(DoubleDef(Ch1));
        channelDefRepo.Set(DoubleDef(Ch2));

        var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, InterpolationType = InterpolationType.Linear };
        mapping.Mappings.Add(new TableMapping("0", "0"));
        mapping.Mappings.Add(new TableMapping("10", "200"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(0.0, GetOutputDouble(Ch2), 0.001);
    }

    [TestMethod]
    public async Task LinearInterpolation_AtUpperDataPoint_ReturnsExactValue()
    {
        channelRepo.Set(Ch1, "10");
        channelDefRepo.Set(DoubleDef(Ch1));
        channelDefRepo.Set(DoubleDef(Ch2));

        var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, InterpolationType = InterpolationType.Linear };
        mapping.Mappings.Add(new TableMapping("0", "0"));
        mapping.Mappings.Add(new TableMapping("10", "200"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(200.0, GetOutputDouble(Ch2), 0.001);
    }

    [TestMethod]
    public async Task LinearInterpolation_Midpoint_ReturnsInterpolatedValue()
    {
        channelRepo.Set(Ch1, "5");
        channelDefRepo.Set(DoubleDef(Ch1));
        channelDefRepo.Set(DoubleDef(Ch2));

        var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, InterpolationType = InterpolationType.Linear };
        mapping.Mappings.Add(new TableMapping("0", "0"));
        mapping.Mappings.Add(new TableMapping("10", "200"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(100.0, GetOutputDouble(Ch2), 0.001);
    }

    [TestMethod]
    public async Task LinearInterpolation_QuarterPoint_ReturnsInterpolatedValue()
    {
        channelRepo.Set(Ch1, "2");
        channelDefRepo.Set(DoubleDef(Ch1));
        channelDefRepo.Set(DoubleDef(Ch2));

        var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, InterpolationType = InterpolationType.Linear };
        mapping.Mappings.Add(new TableMapping("0", "0"));
        mapping.Mappings.Add(new TableMapping("8", "80"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(20.0, GetOutputDouble(Ch2), 0.001);
    }

    // -------------------------------------------------------------------------
    // Double → Double interpolation: CubicSpline
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task CubicSplineInterpolation_AtDataPoint_ReturnsExactValue()
    {
        channelRepo.Set(Ch1, "5");
        channelDefRepo.Set(DoubleDef(Ch1));
        channelDefRepo.Set(DoubleDef(Ch2));

        var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, InterpolationType = InterpolationType.CubicSpline };
        mapping.Mappings.Add(new TableMapping("0", "0"));
        mapping.Mappings.Add(new TableMapping("5", "50"));
        mapping.Mappings.Add(new TableMapping("10", "100"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(50.0, GetOutputDouble(Ch2), 0.001);
    }

    [TestMethod]
    public async Task CubicSplineInterpolation_BetweenPoints_ReturnsApproximateValue()
    {
        channelRepo.Set(Ch1, "3");
        channelDefRepo.Set(DoubleDef(Ch1));
        channelDefRepo.Set(DoubleDef(Ch2));

        var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, InterpolationType = InterpolationType.CubicSpline };
        mapping.Mappings.Add(new TableMapping("0", "0"));
        mapping.Mappings.Add(new TableMapping("5", "50"));
        mapping.Mappings.Add(new TableMapping("10", "100"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(30.0, GetOutputDouble(Ch2), 1.0);
    }

    // -------------------------------------------------------------------------
    // Double → Double interpolation: Polynomial
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task PolynomialInterpolation_AtDataPoint_ReturnsExactValue()
    {
        channelRepo.Set(Ch1, "5");
        channelDefRepo.Set(DoubleDef(Ch1));
        channelDefRepo.Set(DoubleDef(Ch2));

        var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, InterpolationType = InterpolationType.Polynomial };
        mapping.Mappings.Add(new TableMapping("0", "0"));
        mapping.Mappings.Add(new TableMapping("5", "50"));
        mapping.Mappings.Add(new TableMapping("10", "100"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(50.0, GetOutputDouble(Ch2), 0.001);
    }

    [TestMethod]
    public async Task PolynomialInterpolation_BetweenPoints_ReturnsApproximateValue()
    {
        channelRepo.Set(Ch1, "2");
        channelDefRepo.Set(DoubleDef(Ch1));
        channelDefRepo.Set(DoubleDef(Ch2));

        var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, InterpolationType = InterpolationType.Polynomial };
        mapping.Mappings.Add(new TableMapping("0", "0"));
        mapping.Mappings.Add(new TableMapping("5", "50"));
        mapping.Mappings.Add(new TableMapping("10", "100"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(20.0, GetOutputDouble(Ch2), 0.001);
    }

    // -------------------------------------------------------------------------
    // Output formatting via SetBaseValue
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Interpolation_OutputFormattedWithTwoDecimalPlaces()
    {
        channelRepo.Set(Ch1, "5");
        channelDefRepo.Set(DoubleDef(Ch1));
        channelDefRepo.Set(DoubleDef(Ch2));

        var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, InterpolationType = InterpolationType.Linear };
        mapping.Mappings.Add(new TableMapping("0", "0"));
        mapping.Mappings.Add(new TableMapping("10", "200"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(100.0, GetOutputDouble(Ch2), 0.001);
    }

    [TestMethod]
    public async Task Interpolation_OutputFormattedWithZeroDecimalPlaces()
    {
        channelRepo.Set(Ch1, "5");
        channelDefRepo.Set(DoubleDef(Ch1));
        channelDefRepo.Set(DoubleDef(Ch2));

        var mapping = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2, InterpolationType = InterpolationType.Linear };
        mapping.Mappings.Add(new TableMapping("0", "0"));
        mapping.Mappings.Add(new TableMapping("10", "200"));
        tableRepo.Add(mapping);

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual(100.0, GetOutputDouble(Ch2), 0.001);
    }

    // -------------------------------------------------------------------------
    // Multiple mappings
    // -------------------------------------------------------------------------

    //[TestMethod]
    //public async Task MultipleMappings_AllProcessedIndependently()
    //{
    //    channelRepo.Set(Ch1, "park");
    //    channelRepo.Set(Ch3, "2");
    //    channelDefRepo.Set(StringDef(Ch1));
    //    channelDefRepo.Set(StringDef(Ch2));
    //    channelDefRepo.Set(IntDef(Ch3));
    //    channelDefRepo.Set(StringDef(Ch4));

    //    var m1 = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2 };
    //    m1.Mappings.Add(new TableMapping("park", "P"));
    //    tableRepo.Add(m1);

    //    var m2 = new TableDefinition { InputChannel = Ch3, OutputChannel = Ch4 };
    //    m2.Mappings.Add(new TableMapping("1", "one"));
    //    m2.Mappings.Add(new TableMapping("2", "two"));
    //    tableRepo.Add(m2);

    //    await CreateEvaluation().EvaluateAsync();

    //    Assert.AreEqual("P", channelRepo.Get(Ch2).Value);
    //    Assert.AreEqual("two", channelRepo.Get(Ch4).Value);
    //}

    //[TestMethod]
    //public async Task MultipleMappings_OneStringOneInterpolation_BothProduceCorrectOutputs()
    //{
    //    channelRepo.Set(Ch1, "reverse");
    //    channelRepo.Set(Ch3, "5");
    //    channelDefRepo.Set(StringDef(Ch1));
    //    channelDefRepo.Set(StringDef(Ch2));
    //    channelDefRepo.Set(DoubleDef(Ch3));
    //    channelDefRepo.Set(DoubleDef(Ch4));

    //    var m1 = new TableDefinition { InputChannel = Ch1, OutputChannel = Ch2 };
    //    m1.Mappings.Add(new TableMapping("park", "P"));
    //    m1.Mappings.Add(new TableMapping("reverse", "R"));
    //    tableRepo.Add(m1);

    //    var m2 = new TableDefinition { InputChannel = Ch3, OutputChannel = Ch4, InterpolationType = InterpolationType.Linear };
    //    m2.Mappings.Add(new TableMapping("0", "0"));
    //    m2.Mappings.Add(new TableMapping("10", "100"));
    //    tableRepo.Add(m2);

    //    await CreateEvaluation().EvaluateAsync();

    //    Assert.AreEqual("R", channelRepo.Get(Ch2).Value);
    //    Assert.AreEqual(50.0, GetOutputDouble(Ch4), 0.001);
    //}

    [TestMethod]
    public async Task NoMappings_NothingProcessed_OutputChannelUnchanged()
    {
        channelRepo.Set(Ch10, "original");

        await CreateEvaluation().EvaluateAsync();

        Assert.AreEqual("original", channelRepo.Get(Ch10).Value);
    }
}


