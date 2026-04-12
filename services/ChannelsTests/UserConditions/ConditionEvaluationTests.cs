using Channels;
using Channels.Logic;
using Channels.UserConditions;

namespace ChannelsTests.UserConditions;

[TestClass]
public class ConditionEvaluationTests
{
    private static readonly Guid Ch1   = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid ChOut  = new("00000000-0000-0000-0000-000000000010");
    private static readonly Guid ChOut2 = new("00000000-0000-0000-0000-000000000014");

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private ConditionMemoryRepository conditionRepo = null!;
    private ChannelMemoryRepository channelRepo = null!;
    private ChannelDefinitionMemoryRepository channelDefRepo = null!;
    private StatementMemoryRepository statementRepo = null!;

    [TestInitialize]
    public void Setup()
    {
        conditionRepo = new ConditionMemoryRepository();
        channelRepo = new ChannelMemoryRepository();
        channelDefRepo = new ChannelDefinitionMemoryRepository();
        statementRepo = new StatementMemoryRepository();
    }

    private ConditionEvaluation CreateEvaluation() =>
        new(conditionRepo, channelRepo, channelDefRepo, statementRepo);

    private static Guid ConditionId(int id) => new($"00000000-0000-0000-0004-{id:000000000000}");
    private static Guid StatementId(int id) => new($"00000000-0000-0000-0001-{id:000000000000}");
    private static Guid ComparisonId(int id) => new($"00000000-0000-0000-0000-{id:000000000000}");

    private static StatementDefinition AlwaysTrueStatement(int id) =>
        new() { Id = StatementId(id), ActivateComparisons = [[new ComparisonDefinition { Id = ComparisonId(id), ChannelId = Ch1, Logic = LogicType.True }]] };

    private static StatementDefinition AlwaysFalseStatement(int id) =>
        new() { Id = StatementId(id), ActivateComparisons = [[new ComparisonDefinition { Id = ComparisonId(id), ChannelId = Ch1, Logic = LogicType.False }]] };

    private void AddCondition(int id, Guid outputChannelId, params StatementDefinition[] statementDefinitions)
    {
        var def = new ConditionDefinition { Id = ConditionId(id), OutputChannelId = outputChannelId };
        def.Statements.AddRange(statementDefinitions);
        conditionRepo.Add(def);
        foreach (var statementDefinition in statementDefinitions)
            statementRepo.Add(statementDefinition);
    }

    private string GetChannelValue(Guid channelId) => channelRepo.Get(channelId).Value;

    // -------------------------------------------------------------------------
    // Output channel
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task SingleStatement_AlwaysTrue_WritesOneToOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: ChOut, AlwaysTrueStatement(1));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("1", GetChannelValue(ChOut));
    }

    [TestMethod]
    public async Task SingleStatement_AlwaysFalse_WritesZeroToOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: ChOut, AlwaysFalseStatement(1));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("0", GetChannelValue(ChOut));
    }

    [TestMethod]
    public async Task NoStatements_WritesOneToOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: ChOut);

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("1", GetChannelValue(ChOut));
    }

    [TestMethod]
    public async Task OutputChannelIdZero_ChannelNotWritten()
    {
        AddCondition(id: 1, outputChannelId: Guid.Empty, AlwaysTrueStatement(1));

        await CreateEvaluation().UpdateAsync();

        Assert.IsFalse(channelRepo.HasChannel(Guid.Empty));
    }

    // -------------------------------------------------------------------------
    // AND logic across statements
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task MultipleStatements_AllTrue_WritesOneToOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: ChOut, AlwaysTrueStatement(1), AlwaysTrueStatement(2));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("1", GetChannelValue(ChOut));
    }

    [TestMethod]
    public async Task MultipleStatements_FirstStatementFalse_WritesZeroToOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: ChOut, AlwaysFalseStatement(1), AlwaysTrueStatement(2));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("0", GetChannelValue(ChOut));
    }

    [TestMethod]
    public async Task MultipleStatements_LastStatementFalse_WritesZeroToOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: ChOut, AlwaysTrueStatement(1), AlwaysFalseStatement(2));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("0", GetChannelValue(ChOut));
    }

    // -------------------------------------------------------------------------
    // Condition state persistence
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task StatementTrue_ConditionStateIsTrueIsSet()
    {
        AddCondition(id: 1, outputChannelId: ChOut, AlwaysTrueStatement(1));

        await CreateEvaluation().UpdateAsync();

        Assert.IsTrue(conditionRepo.GetState(ConditionId(1))!.IsTrue);
    }

    [TestMethod]
    public async Task StatementFalse_ConditionStateIsTrueIsFalse()
    {
        AddCondition(id: 1, outputChannelId: ChOut, AlwaysFalseStatement(1));

        await CreateEvaluation().UpdateAsync();

        Assert.IsFalse(conditionRepo.GetState(ConditionId(1))!.IsTrue);
    }

    [TestMethod]
    public async Task ConditionState_ConditionIdMatchesDefinition()
    {
        AddCondition(id: 5, outputChannelId: ChOut, AlwaysTrueStatement(1));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual(ConditionId(5), conditionRepo.GetState(ConditionId(5))!.Id);
    }

    // -------------------------------------------------------------------------
    // Multiple conditions
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task MultipleConditions_EachWritesToOwnOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: ChOut,  AlwaysTrueStatement(1));
        AddCondition(id: 2, outputChannelId: ChOut2, AlwaysFalseStatement(2));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("1", GetChannelValue(ChOut));
        Assert.AreEqual("0", GetChannelValue(ChOut2));
    }

    [TestMethod]
    public async Task MultipleConditions_StatesStoredIndependently()
    {
        AddCondition(id: 1, outputChannelId: ChOut,  AlwaysTrueStatement(1));
        AddCondition(id: 2, outputChannelId: ChOut2, AlwaysFalseStatement(2));

        await CreateEvaluation().UpdateAsync();

        Assert.IsTrue(conditionRepo.GetState(ConditionId(1))!.IsTrue);
        Assert.IsFalse(conditionRepo.GetState(ConditionId(2))!.IsTrue);
    }
}

