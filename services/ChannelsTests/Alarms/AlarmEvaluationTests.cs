using System;
using System.Collections.Generic;
using System.Text;
using Channels;
using Channels.Alarms;
using Channels.Logic;

namespace ChannelsTests.Alarms;

[TestClass]
public class AlarmEvaluationTests
{
    private static readonly Guid AlarmId = new("00000000-0000-0000-0000-000000000101");
    private static readonly Guid StatusCh = new("00000000-0000-0000-0000-000000000201");
    private static readonly Guid TriggerCh = new("00000000-0000-0000-0000-000000000301");

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset current;

        public FakeTimeProvider(DateTimeOffset start) => current = start;

        public override DateTimeOffset GetUtcNow() => current;
    }

    private AlarmMemoryRepository alarmRepo = null!;
    private ChannelMemoryRepository channelRepo = null!;
    private ChannelDefinitionMemoryRepository channelDefRepo = null!;
    private StatementMemoryRepository statementRepo = null!;
    private FakeTimeProvider timeProvider = null!;

    [TestInitialize]
    public void Setup()
    {
        alarmRepo = new AlarmMemoryRepository();
        channelRepo = new ChannelMemoryRepository();
        channelDefRepo = new ChannelDefinitionMemoryRepository();
        statementRepo = new StatementMemoryRepository();
        timeProvider = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    private AlarmEvaluation CreateEvaluation() =>
        new(alarmRepo, channelRepo, channelDefRepo, statementRepo, timeProvider);

    private static Guid StatementId(int id) => new($"00000000-0000-0000-0001-{id:000000000000}");
    private static Guid ComparisonId(int id) => new($"00000000-0000-0000-0000-{id:000000000000}");

    private static StatementDefinition AlwaysTrueStatement(int id) =>
        new() { Id = StatementId(id), ActivateComparisons = [[new ComparisonDefinition { Id = ComparisonId(id), ChannelId = TriggerCh, Logic = LogicType.True }]] };

    private static StatementDefinition AlwaysFalseStatement(int id) =>
        new() { Id = StatementId(id), ActivateComparisons = [[new ComparisonDefinition { Id = ComparisonId(id), ChannelId = TriggerCh, Logic = LogicType.False }]] };

    [TestMethod]
    public async Task ActivateTrue_SetsAlarmActive_AndWritesStatusChannel()
    {
        var alarm = new AlarmDefinition { Id = AlarmId, AlarmStatusChannelId = StatusCh };
        alarm.Statements.Add(AlwaysTrueStatement(1));
        alarmRepo.Add(alarm);

        await CreateEvaluation().UpdateAlarmsAsync();

        Assert.IsTrue(alarmRepo.GetState(AlarmId)!.IsActive);
        Assert.AreEqual("1", channelRepo.Get(StatusCh).Value);
    }

    [TestMethod]
    public async Task NoDeactivateStatements_ActivateFalse_DeactivatesAlarm_AndClearsAcknowledged()
    {
        var alarm = new AlarmDefinition { Id = AlarmId, AlarmStatusChannelId = StatusCh };
        alarm.Statements.Add(AlwaysFalseStatement(1));
        alarmRepo.Add(alarm);
        alarmRepo.SetState(new AlarmState
        {
            Id = AlarmId,
            IsActive = true,
            IsAcknowledged = true,
            LastAcknowledgedTimestamp = timeProvider.GetUtcNow().AddSeconds(-5).UtcDateTime,
        });

        await CreateEvaluation().UpdateAlarmsAsync();

        var state = alarmRepo.GetState(AlarmId)!;
        Assert.IsFalse(state.IsActive);
        Assert.IsFalse(state.IsAcknowledged);
        Assert.AreEqual(default, state.LastAcknowledgedTimestamp);
        Assert.AreEqual("0", channelRepo.Get(StatusCh).Value);
    }

    [TestMethod]
    public async Task StatementWithDeactivateComparisons_DeactivatesWhenDeactivateConditionTrue()
    {
        var statement = AlwaysFalseStatement(1);
        statement.DeactivateComparisons = [[new ComparisonDefinition { Id = ComparisonId(2), ChannelId = TriggerCh, Logic = LogicType.True }]];

        var alarm = new AlarmDefinition { Id = AlarmId };
        alarm.Statements.Add(statement);
        alarmRepo.Add(alarm);
        alarmRepo.SetState(new AlarmState { Id = AlarmId, IsActive = true });

        await CreateEvaluation().UpdateAlarmsAsync();

        Assert.IsFalse(alarmRepo.GetState(AlarmId)!.IsActive);
    }

    [TestMethod]
    public async Task ActiveAndAcknowledged_PastAckDelay_ClearsAcknowledged()
    {
        var alarm = new AlarmDefinition { Id = AlarmId, TimeAfterAckToDisplaySecs = 10 };
        alarm.Statements.Add(AlwaysTrueStatement(1));
        alarmRepo.Add(alarm);
        alarmRepo.SetState(new AlarmState
        {
            Id = AlarmId,
            IsActive = true,
            IsAcknowledged = true,
            LastAcknowledgedTimestamp = timeProvider.GetUtcNow().AddSeconds(-11).UtcDateTime,
        });

        await CreateEvaluation().UpdateAlarmsAsync();

        var state = alarmRepo.GetState(AlarmId)!;
        Assert.IsTrue(state.IsActive);
        Assert.IsFalse(state.IsAcknowledged);
        Assert.AreEqual(default, state.LastAcknowledgedTimestamp);
    }
}
