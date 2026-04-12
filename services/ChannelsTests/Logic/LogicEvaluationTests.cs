using Channels;
using Channels.Logic;

namespace ChannelsTests.Logic;

[TestClass]
public class LogicEvaluationTests
{
    private static readonly Guid Ch1 = new("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Ch2 = new("00000000-0000-0000-0000-000000000002");

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

    private ChannelMemoryRepository channelRepo = null!;
    private ChannelDefinitionMemoryRepository channelDefRepo = null!;
    private StatementMemoryRepository statementRepo = null!;
    private FakeTimeProvider timeProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        channelRepo = new ChannelMemoryRepository();
        channelDefRepo = new ChannelDefinitionMemoryRepository();
        statementRepo = new StatementMemoryRepository();
        timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    private LogicEvaluation CreateEvaluation() =>
        new(channelRepo, channelDefRepo, statementRepo, timeProvider: timeProvider);

    // Registers a channel value and its definition together.
    private void SetupChannel(Guid id, string value, string unit = "")
    {
        channelRepo.Set(id, value);
        channelDefRepo.Set(new ChannelDefinition { Id = id, BaseUnitType = unit });
    }

    // Creates a comparison against a static value.
    private static ComparisonDefinition StaticComparison(Guid channelId, LogicType logic, string staticValue, int comparisonId = 1) =>
        new()
        {
            Id = comparisonId,
            ChannelId = channelId,
            Logic = logic,
            UseStaticComparison = true,
            StaticValueComparison = staticValue,
        };

    // Creates a comparison against another channel.
    private static ComparisonDefinition ChannelComparison(Guid channelId, LogicType logic, Guid compareChannelId, int comparisonId = 1) =>
        new()
        {
            Id = comparisonId,
            ChannelId = channelId,
            Logic = logic,
            UseStaticComparison = false,
            ChannelComparisonId = compareChannelId,
        };

    // Wraps a single comparison in a single-group statement.
    private void SetStatement(ComparisonDefinition comparison) =>
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [[comparison]] });

    // -------------------------------------------------------------------------
    // LogicType.True / LogicType.False
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task LogicTrue_ReturnsTrue()
    {
        SetupChannel(Ch1, "0");
        SetStatement(new ComparisonDefinition { Id = 1, ChannelId = Ch1, Logic = LogicType.True });

        bool result = await CreateEvaluation().EvaluateAsync(1);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task LogicFalse_ReturnsFalse()
    {
        SetupChannel(Ch1, "0");
        SetStatement(new ComparisonDefinition { Id = 1, ChannelId = Ch1, Logic = LogicType.False });

        bool result = await CreateEvaluation().EvaluateAsync(1);

        Assert.IsFalse(result);
    }

    // -------------------------------------------------------------------------
    // Relational — static value comparisons
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task GreaterThan_ValueIsGreater_ReturnsTrue()
    {
        SetupChannel(Ch1, "10");
        SetStatement(StaticComparison(Ch1, LogicType.GreaterThan, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task GreaterThan_ValueIsLess_ReturnsFalse()
    {
        SetupChannel(Ch1, "3");
        SetStatement(StaticComparison(Ch1, LogicType.GreaterThan, "5"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task GreaterThan_ValueIsEqual_ReturnsFalse()
    {
        SetupChannel(Ch1, "5");
        SetStatement(StaticComparison(Ch1, LogicType.GreaterThan, "5"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task LessThan_ValueIsLess_ReturnsTrue()
    {
        SetupChannel(Ch1, "3");
        SetStatement(StaticComparison(Ch1, LogicType.LessThan, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task LessThan_ValueIsGreater_ReturnsFalse()
    {
        SetupChannel(Ch1, "10");
        SetStatement(StaticComparison(Ch1, LogicType.LessThan, "5"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task GreaterThanOrEqualTo_ValueIsEqual_ReturnsTrue()
    {
        SetupChannel(Ch1, "5");
        SetStatement(StaticComparison(Ch1, LogicType.GreaterThanOrEqualTo, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task GreaterThanOrEqualTo_ValueIsGreater_ReturnsTrue()
    {
        SetupChannel(Ch1, "6");
        SetStatement(StaticComparison(Ch1, LogicType.GreaterThanOrEqualTo, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task GreaterThanOrEqualTo_ValueIsLess_ReturnsFalse()
    {
        SetupChannel(Ch1, "4");
        SetStatement(StaticComparison(Ch1, LogicType.GreaterThanOrEqualTo, "5"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task LessThanOrEqualTo_ValueIsEqual_ReturnsTrue()
    {
        SetupChannel(Ch1, "5");
        SetStatement(StaticComparison(Ch1, LogicType.LessThanOrEqualTo, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task LessThanOrEqualTo_ValueIsLess_ReturnsTrue()
    {
        SetupChannel(Ch1, "4");
        SetStatement(StaticComparison(Ch1, LogicType.LessThanOrEqualTo, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task EqualTo_ValueIsEqual_ReturnsTrue()
    {
        SetupChannel(Ch1, "42");
        SetStatement(StaticComparison(Ch1, LogicType.EqualTo, "42"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task EqualTo_ValueIsNotEqual_ReturnsFalse()
    {
        SetupChannel(Ch1, "42");
        SetStatement(StaticComparison(Ch1, LogicType.EqualTo, "43"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    // -------------------------------------------------------------------------
    // ReverseResult
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task ReverseResult_TrueComparison_ReturnsFalse()
    {
        SetupChannel(Ch1, "10");
        var comparison = StaticComparison(Ch1, LogicType.GreaterThan, "5");
        comparison.ReverseResult = true;
        SetStatement(comparison);

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ReverseResult_FalseComparison_ReturnsTrue()
    {
        SetupChannel(Ch1, "3");
        var comparison = StaticComparison(Ch1, LogicType.GreaterThan, "5");
        comparison.ReverseResult = true;
        SetStatement(comparison);

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    // -------------------------------------------------------------------------
    // Channel-vs-channel with unit conversion
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task ChannelComparison_SameUnit_GreaterThan_ReturnsTrue()
    {
        SetupChannel(Ch1, "100", "cm");
        SetupChannel(Ch2, "50", "cm");
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [[ChannelComparison(Ch1, LogicType.GreaterThan, Ch2)]] });

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChannelComparison_CompatibleUnits_ConvertsBeforeComparing_ReturnsTrue()
    {
        // 1 km > 500 cm (= 5 m) → true
        SetupChannel(Ch1, "1", "km");
        SetupChannel(Ch2, "500", "cm");
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [[ChannelComparison(Ch1, LogicType.GreaterThan, Ch2)]] });

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChannelComparison_CompatibleUnits_ConvertsBeforeComparing_ReturnsFalse()
    {
        // 100 cm < 1 km → GreaterThan is false
        SetupChannel(Ch1, "100", "cm");
        SetupChannel(Ch2, "1", "km");
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [[ChannelComparison(Ch1, LogicType.GreaterThan, Ch2)]] });

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChannelComparison_BothDimensionless_ComparesRawValues_ReturnsTrue()
    {
        SetupChannel(Ch1, "5", "");
        SetupChannel(Ch2, "3", "");
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [[ChannelComparison(Ch1, LogicType.GreaterThan, Ch2)]] });

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChannelComparison_IncompatibleUnits_ThrowsIncompatibleUnitException()
    {
        SetupChannel(Ch1, "100", "cm");  // Length
        SetupChannel(Ch2, "100", "kg");  // Mass
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [[ChannelComparison(Ch1, LogicType.GreaterThan, Ch2)]] });

        await Assert.ThrowsAsync<IncompatibleUnitException>(
            () => CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChannelComparison_UnitVsDimensionless_ThrowsIncompatibleUnitException()
    {
        SetupChannel(Ch1, "100", "m");
        SetupChannel(Ch2, "50", "");
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [[ChannelComparison(Ch1, LogicType.GreaterThan, Ch2)]] });

        await Assert.ThrowsAsync<IncompatibleUnitException>(
            () => CreateEvaluation().EvaluateAsync(1));
    }

    // -------------------------------------------------------------------------
    // OR / AND grouping
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task OrLogic_FirstGroupTrue_ReturnsTrue()
    {
        SetupChannel(Ch1, "10");
        SetStatement(StaticComparison(Ch1, LogicType.GreaterThan, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task OrLogic_FirstGroupFails_SecondGroupPasses_ReturnsTrue()
    {
        SetupChannel(Ch1, "3");
        SetupChannel(Ch2, "10");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [
                [StaticComparison(Ch1, LogicType.GreaterThan, "5", comparisonId: 1)],  // false
                [StaticComparison(Ch2, LogicType.GreaterThan, "5", comparisonId: 2)],  // true
            ],
        });

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task OrLogic_AllGroupsFail_ReturnsFalse()
    {
        SetupChannel(Ch1, "3");
        SetupChannel(Ch2, "4");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [
                [StaticComparison(Ch1, LogicType.GreaterThan, "5", comparisonId: 1)],  // false
                [StaticComparison(Ch2, LogicType.GreaterThan, "5", comparisonId: 2)],  // false
            ],
        });

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task AndLogic_AllComparisonsInGroupPass_ReturnsTrue()
    {
        SetupChannel(Ch1, "10");
        SetupChannel(Ch2, "20");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [[
                StaticComparison(Ch1, LogicType.GreaterThan, "5", comparisonId: 1),   // true
                StaticComparison(Ch2, LogicType.GreaterThan, "15", comparisonId: 2),  // true
            ]],
        });

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task AndLogic_OneComparisonInGroupFails_ReturnsFalse()
    {
        SetupChannel(Ch1, "10");
        SetupChannel(Ch2, "3");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [[
                StaticComparison(Ch1, LogicType.GreaterThan, "5", comparisonId: 1),  // true
                StaticComparison(Ch2, LogicType.GreaterThan, "5", comparisonId: 2),  // false
            ]],
        });

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task EmptyGroup_IsSkipped_SubsequentGroupEvaluated()
    {
        SetupChannel(Ch1, "10");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [
                [],  // empty — must be skipped, not treated as vacuous true
                [StaticComparison(Ch1, LogicType.GreaterThan, "5", comparisonId: 1)],  // true
            ],
        });

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task NoGroups_ReturnsFalse()
    {
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [] });

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    // -------------------------------------------------------------------------
    // Activate / Deactivate state
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task NoDeactivate_ActivateTrue_ReturnsTrue()
    {
        SetupChannel(Ch1, "10");
        SetStatement(StaticComparison(Ch1, LogicType.GreaterThan, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task NoDeactivate_ActivateFalse_ReturnsFalse()
    {
        SetupChannel(Ch1, "3");
        SetStatement(StaticComparison(Ch1, LogicType.GreaterThan, "5"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task WithDeactivate_DeactivateFiresFirst_ReturnsFalse()
    {
        SetupChannel(Ch1, "10");
        SetupChannel(Ch2, "10");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons   = [[StaticComparison(Ch1, LogicType.GreaterThan, "5", comparisonId: 1)]],
            DeactivateComparisons = [[StaticComparison(Ch2, LogicType.GreaterThan, "5", comparisonId: 2)]],
        });

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task WithDeactivate_OnlyActivateFires_ReturnsTrue()
    {
        SetupChannel(Ch1, "10");
        SetupChannel(Ch2, "3");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons   = [[StaticComparison(Ch1, LogicType.GreaterThan, "5", comparisonId: 1)]],
            DeactivateComparisons = [[StaticComparison(Ch2, LogicType.GreaterThan, "5", comparisonId: 2)]],
        });

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task WithDeactivate_NeitherFires_DefaultsToFalse()
    {
        SetupChannel(Ch1, "3");
        SetupChannel(Ch2, "3");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons   = [[StaticComparison(Ch1, LogicType.GreaterThan, "5", comparisonId: 1)]],
            DeactivateComparisons = [[StaticComparison(Ch2, LogicType.GreaterThan, "5", comparisonId: 2)]],
        });

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task WithDeactivate_NeitherFires_RetainsPreviousActivatedState()
    {
        SetupChannel(Ch1, "10");
        SetupChannel(Ch2, "3");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons   = [[StaticComparison(Ch1, LogicType.GreaterThan, "5", comparisonId: 1)]],
            DeactivateComparisons = [[StaticComparison(Ch2, LogicType.GreaterThan, "5", comparisonId: 2)]],
        });

        var evaluation = CreateEvaluation();

        bool first = await evaluation.EvaluateAsync(1);
        Assert.IsTrue(first);

        channelRepo.Set(Ch1, "3");

        bool second = await evaluation.EvaluateAsync(1);
        Assert.IsTrue(second);
    }

    [TestMethod]
    public async Task WithDeactivate_DeactivateFires_AfterActivated_ReturnsFalse()
    {
        SetupChannel(Ch1, "10");
        SetupChannel(Ch2, "3");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons   = [[StaticComparison(Ch1, LogicType.GreaterThan, "5", comparisonId: 1)]],
            DeactivateComparisons = [[StaticComparison(Ch2, LogicType.GreaterThan, "5", comparisonId: 2)]],
        });

        var evaluation = CreateEvaluation();

        bool activated = await evaluation.EvaluateAsync(1);
        Assert.IsTrue(activated);

        channelRepo.Set(Ch2, "10");
        bool deactivated = await evaluation.EvaluateAsync(1);
        Assert.IsFalse(deactivated);
    }

    // -------------------------------------------------------------------------
    // ForMs — duration requirement
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task ForMs_ConditionFirstBecomesTrueOnFirstCall_ReturnsFalse()
    {
        SetupChannel(Ch1, "10");
        var comparison = StaticComparison(Ch1, LogicType.GreaterThan, "5");
        comparison.ForMs = 1000;
        SetStatement(comparison);

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ForMs_ConditionTrueForRequiredDuration_ReturnsTrue()
    {
        SetupChannel(Ch1, "10");
        var comparison = StaticComparison(Ch1, LogicType.GreaterThan, "5");
        comparison.ForMs = 1000;
        SetStatement(comparison);

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);

        timeProvider.Advance(TimeSpan.FromMilliseconds(1001));

        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ForMs_ConditionTrueButDurationNotYetMet_ReturnsFalse()
    {
        SetupChannel(Ch1, "10");
        var comparison = StaticComparison(Ch1, LogicType.GreaterThan, "5");
        comparison.ForMs = 1000;
        SetStatement(comparison);

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);

        timeProvider.Advance(TimeSpan.FromMilliseconds(500));

        Assert.IsFalse(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ForMs_ConditionBecomesFalse_TimerCleared_ReturnsFalse()
    {
        SetupChannel(Ch1, "10");
        var comparison = StaticComparison(Ch1, LogicType.GreaterThan, "5");
        comparison.ForMs = 1000;
        SetStatement(comparison);

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);
        timeProvider.Advance(TimeSpan.FromMilliseconds(500));

        channelRepo.Set(Ch1, "3");
        bool falseResult = await evaluation.EvaluateAsync(1);
        Assert.IsFalse(falseResult);

        channelRepo.Set(Ch1, "10");
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        Assert.IsFalse(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ForMs_TimerRestartsAfterReset_EventuallyReturnsTrue()
    {
        SetupChannel(Ch1, "10");
        var comparison = StaticComparison(Ch1, LogicType.GreaterThan, "5");
        comparison.ForMs = 1000;
        SetStatement(comparison);

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);

        channelRepo.Set(Ch1, "3");
        await evaluation.EvaluateAsync(1);

        channelRepo.Set(Ch1, "10");
        await evaluation.EvaluateAsync(1);

        timeProvider.Advance(TimeSpan.FromMilliseconds(1001));

        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    // -------------------------------------------------------------------------
    // LogicType.Updated
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Updated_FirstEvaluation_ReturnsFalse()
    {
        SetupChannel(Ch1, "100");
        SetStatement(new ComparisonDefinition { Id = 1, ChannelId = Ch1, Logic = LogicType.Updated });

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task Updated_ValueUnchanged_ReturnsFalse()
    {
        SetupChannel(Ch1, "100");
        SetStatement(new ComparisonDefinition { Id = 1, ChannelId = Ch1, Logic = LogicType.Updated });

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);

        Assert.IsFalse(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task Updated_ValueChanged_ReturnsTrue()
    {
        SetupChannel(Ch1, "100");
        SetStatement(new ComparisonDefinition { Id = 1, ChannelId = Ch1, Logic = LogicType.Updated });

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);

        channelRepo.Set(Ch1, "200");
        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task Updated_ValueChangedThenRestored_DetectsEachChange()
    {
        SetupChannel(Ch1, "100");
        SetStatement(new ComparisonDefinition { Id = 1, ChannelId = Ch1, Logic = LogicType.Updated });

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);

        channelRepo.Set(Ch1, "200");
        Assert.IsTrue(await evaluation.EvaluateAsync(1));

        channelRepo.Set(Ch1, "200");
        Assert.IsFalse(await evaluation.EvaluateAsync(1));

        channelRepo.Set(Ch1, "100");
        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    // -------------------------------------------------------------------------
    // LogicType.ChangedBy
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task ChangedBy_FirstEvaluation_ReturnsFalse()
    {
        SetupChannel(Ch1, "100");
        SetStatement(StaticComparison(Ch1, LogicType.ChangedBy, "10"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChangedBy_ChangeBelowThreshold_ReturnsFalse()
    {
        SetupChannel(Ch1, "100");
        SetStatement(StaticComparison(Ch1, LogicType.ChangedBy, "10"));

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);

        channelRepo.Set(Ch1, "105");
        Assert.IsFalse(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChangedBy_ChangeExactlyAtThreshold_ReturnsTrue()
    {
        SetupChannel(Ch1, "100");
        SetStatement(StaticComparison(Ch1, LogicType.ChangedBy, "10"));

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);

        channelRepo.Set(Ch1, "110");
        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChangedBy_ChangeAboveThreshold_ReturnsTrue()
    {
        SetupChannel(Ch1, "100");
        SetStatement(StaticComparison(Ch1, LogicType.ChangedBy, "10"));

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);

        channelRepo.Set(Ch1, "115");
        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChangedBy_NegativeChangeAboveThreshold_ReturnsTrue()
    {
        SetupChannel(Ch1, "100");
        SetStatement(StaticComparison(Ch1, LogicType.ChangedBy, "10"));

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);

        channelRepo.Set(Ch1, "85");
        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChangedBy_ThresholdFromComparisonChannel_UsesChannelValue()
    {
        SetupChannel(Ch1, "100", "");
        SetupChannel(Ch2, "10", "");
        var comparison = new ComparisonDefinition
        {
            Id = 1,
            ChannelId = Ch1,
            Logic = LogicType.ChangedBy,
            UseStaticComparison = false,
            ChannelComparisonId = Ch2,
        };
        SetStatement(comparison);

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);

        channelRepo.Set(Ch1, "115");
        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    // -------------------------------------------------------------------------
    // Exception paths
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Relational_NoStaticOrChannelConfig_ThrowsInvalidOperationException()
    {
        SetupChannel(Ch1, "10");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [[
                new ComparisonDefinition { Id = 1, ChannelId = Ch1, Logic = LogicType.GreaterThan, UseStaticComparison = false },
            ]],
        });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChangedBy_NoStaticOrChannelConfig_ThrowsInvalidOperationException()
    {
        SetupChannel(Ch1, "100");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [[
                new ComparisonDefinition { Id = 1, ChannelId = Ch1, Logic = LogicType.ChangedBy, UseStaticComparison = false },
            ]],
        });

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);  // records initial value — no exception yet

        channelRepo.Set(Ch1, "200");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => evaluation.EvaluateAsync(1));
    }
}


