using Channels;
using Channels.Logic;
using Channels.UserConditions;

namespace ChannelsTests.UserConditions;

[TestClass]
public class ConditionEvaluationTests
{
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

    private static StatementDefinition AlwaysTrueStatement(int id) =>
        new() { Id = id, ActivateComparisons = [[new ComparisonDefinition { ComparisonId = id, ChannelId = 1, Logic = LogicType.True }]] };

    private static StatementDefinition AlwaysFalseStatement(int id) =>
        new() { Id = id, ActivateComparisons = [[new ComparisonDefinition { ComparisonId = id, ChannelId = 1, Logic = LogicType.False }]] };

    private void AddCondition(int id, int outputChannelId, params StatementDefinition[] statementDefinitions)
    {
        var def = new ConditionDefinition { Id = id, OutputChannelId = outputChannelId };
        def.Statements.AddRange(statementDefinitions);
        conditionRepo.Add(def);
        foreach (var statementDefinition in statementDefinitions)
            statementRepo.Add(statementDefinition);
    }

    private string GetChannelValue(int channelId) => channelRepo.Get(channelId).Value;

    // -------------------------------------------------------------------------
    // Output channel
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task SingleStatement_AlwaysTrue_WritesOneToOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: 10, AlwaysTrueStatement(1));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("1", GetChannelValue(10));
    }

    [TestMethod]
    public async Task SingleStatement_AlwaysFalse_WritesZeroToOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: 10, AlwaysFalseStatement(1));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("0", GetChannelValue(10));
    }

    [TestMethod]
    public async Task NoStatements_WritesOneToOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: 10);

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("1", GetChannelValue(10));
    }

    [TestMethod]
    public async Task OutputChannelIdZero_ChannelNotWritten()
    {
        AddCondition(id: 1, outputChannelId: 0, AlwaysTrueStatement(1));

        await CreateEvaluation().UpdateAsync();

        Assert.IsFalse(channelRepo.HasChannel(0));
    }

    // -------------------------------------------------------------------------
    // AND logic across statements
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task MultipleStatements_AllTrue_WritesOneToOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: 10, AlwaysTrueStatement(1), AlwaysTrueStatement(2));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("1", GetChannelValue(10));
    }

    [TestMethod]
    public async Task MultipleStatements_FirstStatementFalse_WritesZeroToOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: 10, AlwaysFalseStatement(1), AlwaysTrueStatement(2));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("0", GetChannelValue(10));
    }

    [TestMethod]
    public async Task MultipleStatements_LastStatementFalse_WritesZeroToOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: 10, AlwaysTrueStatement(1), AlwaysFalseStatement(2));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("0", GetChannelValue(10));
    }

    // -------------------------------------------------------------------------
    // Condition state persistence
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task StatementTrue_ConditionStateIsTrueIsSet()
    {
        AddCondition(id: 1, outputChannelId: 10, AlwaysTrueStatement(1));

        await CreateEvaluation().UpdateAsync();

        Assert.IsTrue(conditionRepo.GetState(1)!.IsTrue);
    }

    [TestMethod]
    public async Task StatementFalse_ConditionStateIsTrueIsFalse()
    {
        AddCondition(id: 1, outputChannelId: 10, AlwaysFalseStatement(1));

        await CreateEvaluation().UpdateAsync();

        Assert.IsFalse(conditionRepo.GetState(1)!.IsTrue);
    }

    [TestMethod]
    public async Task ConditionState_ConditionIdMatchesDefinition()
    {
        AddCondition(id: 5, outputChannelId: 10, AlwaysTrueStatement(1));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual(5, conditionRepo.GetState(5)!.ConditionId);
    }

    // -------------------------------------------------------------------------
    // Multiple conditions
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task MultipleConditions_EachWritesToOwnOutputChannel()
    {
        AddCondition(id: 1, outputChannelId: 10, AlwaysTrueStatement(1));
        AddCondition(id: 2, outputChannelId: 20, AlwaysFalseStatement(2));

        await CreateEvaluation().UpdateAsync();

        Assert.AreEqual("1", GetChannelValue(10));
        Assert.AreEqual("0", GetChannelValue(20));
    }

    [TestMethod]
    public async Task MultipleConditions_StatesStoredIndependently()
    {
        AddCondition(id: 1, outputChannelId: 10, AlwaysTrueStatement(1));
        AddCondition(id: 2, outputChannelId: 20, AlwaysFalseStatement(2));

        await CreateEvaluation().UpdateAsync();

        Assert.IsTrue(conditionRepo.GetState(1)!.IsTrue);
        Assert.IsFalse(conditionRepo.GetState(2)!.IsTrue);
    }
}

