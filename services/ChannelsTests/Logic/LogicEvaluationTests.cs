using Channels;
using Channels.Logic;

namespace ChannelsTests.Logic;

[TestClass]
public class LogicEvaluationTests
{
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
    private void SetupChannel(int id, string value, string unit = "")
    {
        channelRepo.Set(id, value);
        channelDefRepo.Set(new ChannelDefinition { Id = id, BaseUnitType = unit });
    }

    // Creates a comparison against a static value.
    private static ComparisonDefinition StaticComparison(int channelId, LogicType logic, string staticValue, int comparisonId = 1) =>
        new()
        {
            Id = comparisonId,
            ChannelId = channelId,
            Logic = logic,
            UseStaticComparison = true,
            StaticValueComparison = staticValue,
        };

    // Creates a comparison against another channel.
    private static ComparisonDefinition ChannelComparison(int channelId, LogicType logic, int compareChannelId, int comparisonId = 1) =>
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
        SetupChannel(1, "0");
        SetStatement(new ComparisonDefinition { Id = 1, ChannelId = 1, Logic = LogicType.True });

        bool result = await CreateEvaluation().EvaluateAsync(1);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task LogicFalse_ReturnsFalse()
    {
        SetupChannel(1, "0");
        SetStatement(new ComparisonDefinition { Id = 1, ChannelId = 1, Logic = LogicType.False });

        bool result = await CreateEvaluation().EvaluateAsync(1);

        Assert.IsFalse(result);
    }

    // -------------------------------------------------------------------------
    // Relational — static value comparisons
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task GreaterThan_ValueIsGreater_ReturnsTrue()
    {
        SetupChannel(1, "10");
        SetStatement(StaticComparison(1, LogicType.GreaterThan, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task GreaterThan_ValueIsLess_ReturnsFalse()
    {
        SetupChannel(1, "3");
        SetStatement(StaticComparison(1, LogicType.GreaterThan, "5"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task GreaterThan_ValueIsEqual_ReturnsFalse()
    {
        SetupChannel(1, "5");
        SetStatement(StaticComparison(1, LogicType.GreaterThan, "5"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task LessThan_ValueIsLess_ReturnsTrue()
    {
        SetupChannel(1, "3");
        SetStatement(StaticComparison(1, LogicType.LessThan, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task LessThan_ValueIsGreater_ReturnsFalse()
    {
        SetupChannel(1, "10");
        SetStatement(StaticComparison(1, LogicType.LessThan, "5"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task GreaterThanOrEqualTo_ValueIsEqual_ReturnsTrue()
    {
        SetupChannel(1, "5");
        SetStatement(StaticComparison(1, LogicType.GreaterThanOrEqualTo, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task GreaterThanOrEqualTo_ValueIsGreater_ReturnsTrue()
    {
        SetupChannel(1, "6");
        SetStatement(StaticComparison(1, LogicType.GreaterThanOrEqualTo, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task GreaterThanOrEqualTo_ValueIsLess_ReturnsFalse()
    {
        SetupChannel(1, "4");
        SetStatement(StaticComparison(1, LogicType.GreaterThanOrEqualTo, "5"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task LessThanOrEqualTo_ValueIsEqual_ReturnsTrue()
    {
        SetupChannel(1, "5");
        SetStatement(StaticComparison(1, LogicType.LessThanOrEqualTo, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task LessThanOrEqualTo_ValueIsLess_ReturnsTrue()
    {
        SetupChannel(1, "4");
        SetStatement(StaticComparison(1, LogicType.LessThanOrEqualTo, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task EqualTo_ValueIsEqual_ReturnsTrue()
    {
        SetupChannel(1, "42");
        SetStatement(StaticComparison(1, LogicType.EqualTo, "42"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task EqualTo_ValueIsNotEqual_ReturnsFalse()
    {
        SetupChannel(1, "42");
        SetStatement(StaticComparison(1, LogicType.EqualTo, "43"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    // -------------------------------------------------------------------------
    // ReverseResult
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task ReverseResult_TrueComparison_ReturnsFalse()
    {
        SetupChannel(1, "10");
        var comparison = StaticComparison(1, LogicType.GreaterThan, "5");
        comparison.ReverseResult = true;
        SetStatement(comparison);

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ReverseResult_FalseComparison_ReturnsTrue()
    {
        SetupChannel(1, "3");
        var comparison = StaticComparison(1, LogicType.GreaterThan, "5");
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
        SetupChannel(1, "100", "cm");
        SetupChannel(2, "50", "cm");
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [[ChannelComparison(1, LogicType.GreaterThan, 2)]] });

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChannelComparison_CompatibleUnits_ConvertsBeforeComparing_ReturnsTrue()
    {
        // 1 km > 500 cm (= 5 m) → true
        SetupChannel(1, "1", "km");
        SetupChannel(2, "500", "cm");
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [[ChannelComparison(1, LogicType.GreaterThan, 2)]] });

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChannelComparison_CompatibleUnits_ConvertsBeforeComparing_ReturnsFalse()
    {
        // 100 cm < 1 km → GreaterThan is false
        SetupChannel(1, "100", "cm");
        SetupChannel(2, "1", "km");
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [[ChannelComparison(1, LogicType.GreaterThan, 2)]] });

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChannelComparison_BothDimensionless_ComparesRawValues_ReturnsTrue()
    {
        SetupChannel(1, "5", "");
        SetupChannel(2, "3", "");
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [[ChannelComparison(1, LogicType.GreaterThan, 2)]] });

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChannelComparison_IncompatibleUnits_ThrowsIncompatibleUnitException()
    {
        SetupChannel(1, "100", "cm");  // Length
        SetupChannel(2, "100", "kg");  // Mass
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [[ChannelComparison(1, LogicType.GreaterThan, 2)]] });

        await Assert.ThrowsAsync<IncompatibleUnitException>(
            () => CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChannelComparison_UnitVsDimensionless_ThrowsIncompatibleUnitException()
    {
        SetupChannel(1, "100", "m");
        SetupChannel(2, "50", "");
        statementRepo.Set(new StatementDefinition { Id = 1, ActivateComparisons = [[ChannelComparison(1, LogicType.GreaterThan, 2)]] });

        await Assert.ThrowsAsync<IncompatibleUnitException>(
            () => CreateEvaluation().EvaluateAsync(1));
    }

    // -------------------------------------------------------------------------
    // OR / AND grouping
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task OrLogic_FirstGroupTrue_ReturnsTrue()
    {
        SetupChannel(1, "10");
        SetStatement(StaticComparison(1, LogicType.GreaterThan, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task OrLogic_FirstGroupFails_SecondGroupPasses_ReturnsTrue()
    {
        SetupChannel(1, "3");
        SetupChannel(2, "10");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [
                [StaticComparison(1, LogicType.GreaterThan, "5", comparisonId: 1)],  // false
                [StaticComparison(2, LogicType.GreaterThan, "5", comparisonId: 2)],  // true
            ],
        });

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task OrLogic_AllGroupsFail_ReturnsFalse()
    {
        SetupChannel(1, "3");
        SetupChannel(2, "4");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [
                [StaticComparison(1, LogicType.GreaterThan, "5", comparisonId: 1)],  // false
                [StaticComparison(2, LogicType.GreaterThan, "5", comparisonId: 2)],  // false
            ],
        });

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task AndLogic_AllComparisonsInGroupPass_ReturnsTrue()
    {
        SetupChannel(1, "10");
        SetupChannel(2, "20");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [[
                StaticComparison(1, LogicType.GreaterThan, "5", comparisonId: 1),   // true
                StaticComparison(2, LogicType.GreaterThan, "15", comparisonId: 2),  // true
            ]],
        });

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task AndLogic_OneComparisonInGroupFails_ReturnsFalse()
    {
        SetupChannel(1, "10");
        SetupChannel(2, "3");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [[
                StaticComparison(1, LogicType.GreaterThan, "5", comparisonId: 1),  // true
                StaticComparison(2, LogicType.GreaterThan, "5", comparisonId: 2),  // false
            ]],
        });

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task EmptyGroup_IsSkipped_SubsequentGroupEvaluated()
    {
        SetupChannel(1, "10");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [
                [],  // empty — must be skipped, not treated as vacuous true
                [StaticComparison(1, LogicType.GreaterThan, "5", comparisonId: 1)],  // true
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
        SetupChannel(1, "10");
        SetStatement(StaticComparison(1, LogicType.GreaterThan, "5"));

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task NoDeactivate_ActivateFalse_ReturnsFalse()
    {
        SetupChannel(1, "3");
        SetStatement(StaticComparison(1, LogicType.GreaterThan, "5"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task WithDeactivate_DeactivateFiresFirst_ReturnsFalse()
    {
        // Both comparisons fire — deactivate wins
        SetupChannel(1, "10");
        SetupChannel(2, "10");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons = [[StaticComparison(1, LogicType.GreaterThan, "5", comparisonId: 1)]],
            DeactivateComparisons = [[StaticComparison(2, LogicType.GreaterThan, "5", comparisonId: 2)]],
        });

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task WithDeactivate_OnlyActivateFires_ReturnsTrue()
    {
        SetupChannel(1, "10");  // activate: 10 > 5 = true
        SetupChannel(2, "3");   // deactivate: 3 > 5 = false
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons = [[StaticComparison(1, LogicType.GreaterThan, "5", comparisonId: 1)]],
            DeactivateComparisons = [[StaticComparison(2, LogicType.GreaterThan, "5", comparisonId: 2)]],
        });

        Assert.IsTrue(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task WithDeactivate_NeitherFires_DefaultsToFalse()
    {
        SetupChannel(1, "3");  // activate: false
        SetupChannel(2, "3");  // deactivate: false
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons = [[StaticComparison(1, LogicType.GreaterThan, "5", comparisonId: 1)]],
            DeactivateComparisons = [[StaticComparison(2, LogicType.GreaterThan, "5", comparisonId: 2)]],
        });

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task WithDeactivate_NeitherFires_RetainsPreviousActivatedState()
    {
        SetupChannel(1, "10"); // activate channel
        SetupChannel(2, "3");  // deactivate channel (never fires)
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons = [[StaticComparison(1, LogicType.GreaterThan, "5", comparisonId: 1)]],
            DeactivateComparisons = [[StaticComparison(2, LogicType.GreaterThan, "5", comparisonId: 2)]],
        });

        var evaluation = CreateEvaluation();

        // First call: activate fires → state saved as true
        bool first = await evaluation.EvaluateAsync(1);
        Assert.IsTrue(first);

        // Drop channel 1 below threshold so neither comparison fires
        channelRepo.Set(1, "3");

        // Second call: neither fires → retain previous true state
        bool second = await evaluation.EvaluateAsync(1);
        Assert.IsTrue(second);
    }

    [TestMethod]
    public async Task WithDeactivate_DeactivateFires_AfterActivated_ReturnsFalse()
    {
        SetupChannel(1, "10"); // activate channel
        SetupChannel(2, "3");  // deactivate channel
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons = [[StaticComparison(1, LogicType.GreaterThan, "5", comparisonId: 1)]],
            DeactivateComparisons = [[StaticComparison(2, LogicType.GreaterThan, "5", comparisonId: 2)]],
        });

        var evaluation = CreateEvaluation();

        // Activate
        bool activated = await evaluation.EvaluateAsync(1);
        Assert.IsTrue(activated);

        // Now trigger deactivate
        channelRepo.Set(2, "10");
        bool deactivated = await evaluation.EvaluateAsync(1);
        Assert.IsFalse(deactivated);
    }

    // -------------------------------------------------------------------------
    // ForMs — duration requirement
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task ForMs_ConditionFirstBecomesTrueOnFirstCall_ReturnsFalse()
    {
        SetupChannel(1, "10");
        var comparison = StaticComparison(1, LogicType.GreaterThan, "5");
        comparison.ForMs = 1000;
        SetStatement(comparison);

        // Timer just started on the first call
        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ForMs_ConditionTrueForRequiredDuration_ReturnsTrue()
    {
        SetupChannel(1, "10");
        var comparison = StaticComparison(1, LogicType.GreaterThan, "5");
        comparison.ForMs = 1000;
        SetStatement(comparison);

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);  // starts timer

        timeProvider.Advance(TimeSpan.FromMilliseconds(1001));

        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ForMs_ConditionTrueButDurationNotYetMet_ReturnsFalse()
    {
        SetupChannel(1, "10");
        var comparison = StaticComparison(1, LogicType.GreaterThan, "5");
        comparison.ForMs = 1000;
        SetStatement(comparison);

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);  // starts timer

        timeProvider.Advance(TimeSpan.FromMilliseconds(500));  // only half way

        Assert.IsFalse(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ForMs_ConditionBecomesFalse_TimerCleared_ReturnsFalse()
    {
        SetupChannel(1, "10");
        var comparison = StaticComparison(1, LogicType.GreaterThan, "5");
        comparison.ForMs = 1000;
        SetStatement(comparison);

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);       // timer started
        timeProvider.Advance(TimeSpan.FromMilliseconds(500));

        channelRepo.Set(1, "3");                 // condition becomes false
        bool falseResult = await evaluation.EvaluateAsync(1);
        Assert.IsFalse(falseResult);             // timer cleared

        // Even with enough total elapsed time, returning false because timer was reset
        channelRepo.Set(1, "10");
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        Assert.IsFalse(await evaluation.EvaluateAsync(1));  // only 100ms since restart
    }

    [TestMethod]
    public async Task ForMs_TimerRestartsAfterReset_EventuallyReturnsTrue()
    {
        SetupChannel(1, "10");
        var comparison = StaticComparison(1, LogicType.GreaterThan, "5");
        comparison.ForMs = 1000;
        SetStatement(comparison);

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);       // timer started

        channelRepo.Set(1, "3");
        await evaluation.EvaluateAsync(1);       // timer cleared

        channelRepo.Set(1, "10");
        await evaluation.EvaluateAsync(1);       // timer restarted

        timeProvider.Advance(TimeSpan.FromMilliseconds(1001));

        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    // -------------------------------------------------------------------------
    // LogicType.Updated
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Updated_FirstEvaluation_ReturnsFalse()
    {
        SetupChannel(1, "100");
        SetStatement(new ComparisonDefinition { Id = 1, ChannelId = 1, Logic = LogicType.Updated });

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task Updated_ValueUnchanged_ReturnsFalse()
    {
        SetupChannel(1, "100");
        SetStatement(new ComparisonDefinition { Id = 1, ChannelId = 1, Logic = LogicType.Updated });

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);  // records initial value

        Assert.IsFalse(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task Updated_ValueChanged_ReturnsTrue()
    {
        SetupChannel(1, "100");
        SetStatement(new ComparisonDefinition { Id = 1, ChannelId = 1, Logic = LogicType.Updated });

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);  // records initial value

        channelRepo.Set(1, "200");
        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task Updated_ValueChangedThenRestored_DetectsEachChange()
    {
        SetupChannel(1, "100");
        SetStatement(new ComparisonDefinition { Id = 1, ChannelId = 1, Logic = LogicType.Updated });

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);  // records 100

        channelRepo.Set(1, "200");
        Assert.IsTrue(await evaluation.EvaluateAsync(1));  // 100 → 200, changed

        channelRepo.Set(1, "200");
        Assert.IsFalse(await evaluation.EvaluateAsync(1)); // 200 → 200, unchanged

        channelRepo.Set(1, "100");
        Assert.IsTrue(await evaluation.EvaluateAsync(1));  // 200 → 100, changed
    }

    // -------------------------------------------------------------------------
    // LogicType.ChangedBy
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task ChangedBy_FirstEvaluation_ReturnsFalse()
    {
        SetupChannel(1, "100");
        SetStatement(StaticComparison(1, LogicType.ChangedBy, "10"));

        Assert.IsFalse(await CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChangedBy_ChangeBelowThreshold_ReturnsFalse()
    {
        SetupChannel(1, "100");
        SetStatement(StaticComparison(1, LogicType.ChangedBy, "10"));

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);  // records 100

        channelRepo.Set(1, "105");  // changed by 5, threshold is 10
        Assert.IsFalse(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChangedBy_ChangeExactlyAtThreshold_ReturnsTrue()
    {
        SetupChannel(1, "100");
        SetStatement(StaticComparison(1, LogicType.ChangedBy, "10"));

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);  // records 100

        channelRepo.Set(1, "110");  // changed by exactly 10
        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChangedBy_ChangeAboveThreshold_ReturnsTrue()
    {
        SetupChannel(1, "100");
        SetStatement(StaticComparison(1, LogicType.ChangedBy, "10"));

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);

        channelRepo.Set(1, "115");  // changed by 15 > 10
        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChangedBy_NegativeChangeAboveThreshold_ReturnsTrue()
    {
        SetupChannel(1, "100");
        SetStatement(StaticComparison(1, LogicType.ChangedBy, "10"));

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);

        channelRepo.Set(1, "85");   // decreased by 15, abs(15) >= 10
        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChangedBy_ThresholdFromComparisonChannel_UsesChannelValue()
    {
        SetupChannel(1, "100", "");  // main channel
        SetupChannel(2, "10", "");   // threshold channel
        var comparison = new ComparisonDefinition
        {
            Id = 1,
            ChannelId = 1,
            Logic = LogicType.ChangedBy,
            UseStaticComparison = false,
            ChannelComparisonId = 2,
        };
        SetStatement(comparison);

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);  // records 100

        channelRepo.Set(1, "115");  // changed by 15 >= threshold 10
        Assert.IsTrue(await evaluation.EvaluateAsync(1));
    }

    // -------------------------------------------------------------------------
    // Exception paths
    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Relational_NoStaticOrChannelConfig_ThrowsInvalidOperationException()
    {
        SetupChannel(1, "10");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [[
                new ComparisonDefinition { Id = 1, ChannelId = 1, Logic = LogicType.GreaterThan, UseStaticComparison = false },
            ]],
        });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateEvaluation().EvaluateAsync(1));
    }

    [TestMethod]
    public async Task ChangedBy_NoStaticOrChannelConfig_ThrowsInvalidOperationException()
    {
        SetupChannel(1, "100");
        statementRepo.Set(new StatementDefinition
        {
            Id = 1,
            ActivateComparisons =
            [[
                new ComparisonDefinition { Id = 1, ChannelId = 1, Logic = LogicType.ChangedBy, UseStaticComparison = false },
            ]],
        });

        var evaluation = CreateEvaluation();
        await evaluation.EvaluateAsync(1);  // records initial value — no exception yet

        channelRepo.Set(1, "200");
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => evaluation.EvaluateAsync(1));
    }
}


