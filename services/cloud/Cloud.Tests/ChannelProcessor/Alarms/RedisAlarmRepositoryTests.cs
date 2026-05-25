using ChannelProcessor.Alarms.State;
using Channels.Alarms;
using Channels.Logic;
using Cloud.Shared.Alarms;

namespace Cloud.Tests.ChannelProcessor.Alarms;

[TestClass]
public class RedisAlarmRepositoryTests
{
    private const string CarKey = "team-1-car-77";
    private static readonly Guid AlarmId = Guid.Parse("00000000-0000-0000-0000-000000000A11");

    private sealed class FakeGateway : IRedisAlarmStateGateway
    {
        private readonly Dictionary<(string carKey, Guid id), AlarmState> _store = new();

        public Task<AlarmState> GetAsync(string carKey, Guid alarmId, CancellationToken ct = default)
        {
            if (_store.TryGetValue((carKey, alarmId), out var state))
            {
                // Return a copy so the caller's mutations don't bleed into stored state.
                return Task.FromResult(new AlarmState
                {
                    Id = state.Id,
                    IsActive = state.IsActive,
                    IsAcknowledged = state.IsAcknowledged,
                    LastAcknowledgedTimestamp = state.LastAcknowledgedTimestamp,
                });
            }
            return Task.FromResult(new AlarmState { Id = alarmId });
        }

        public Task SetAsync(string carKey, AlarmState state, CancellationToken ct = default)
        {
            _store[(carKey, state.Id)] = new AlarmState
            {
                Id = state.Id,
                IsActive = state.IsActive,
                IsAcknowledged = state.IsAcknowledged,
                LastAcknowledgedTimestamp = state.LastAcknowledgedTimestamp,
            };
            return Task.CompletedTask;
        }

        public Task<AlarmState> AcknowledgeAsync(string carKey, Guid alarmId, DateTime utcNow, CancellationToken ct = default)
            => throw new NotSupportedException("Repo does not invoke acknowledge.");

        public AlarmState? Peek(Guid alarmId) =>
            _store.TryGetValue((CarKey, alarmId), out var s) ? s : null;

        public void Seed(AlarmState state) => _store[(CarKey, state.Id)] = state;
    }

    private FakeGateway _gateway = null!;

    [TestInitialize]
    public void Setup() => _gateway = new FakeGateway();

    private static AlarmDefinition Definition() =>
        new() { Id = AlarmId, Name = "A", Statement = new StatementDefinition() };

    private RedisAlarmRepository CreateRepo() =>
        new(_gateway, CarKey, new[] { Definition() });

    [TestMethod]
    public async Task GetAlarmState_NoStored_ReturnsDefaultStateWithId()
    {
        var repo = CreateRepo();
        var state = await repo.GetAlarmStateAsync(AlarmId);

        Assert.AreEqual(AlarmId, state.Id);
        Assert.IsFalse(state.IsActive);
        Assert.IsFalse(state.IsAcknowledged);
    }

    [TestMethod]
    public async Task GetAlarmState_Stored_ReturnsValuesFromGateway()
    {
        _gateway.Seed(new AlarmState
        {
            Id = AlarmId,
            IsActive = true,
            IsAcknowledged = true,
            LastAcknowledgedTimestamp = new DateTime(2026, 1, 1),
        });

        var repo = CreateRepo();
        var state = await repo.GetAlarmStateAsync(AlarmId);

        Assert.IsTrue(state.IsActive);
        Assert.IsTrue(state.IsAcknowledged);
        Assert.AreEqual(new DateTime(2026, 1, 1), state.LastAcknowledgedTimestamp);
    }

    [TestMethod]
    public async Task SetAlarmState_PersistsThroughGateway()
    {
        var repo = CreateRepo();
        await repo.SetAlarmStateAsync(new AlarmState { Id = AlarmId, IsActive = true });

        var stored = _gateway.Peek(AlarmId);
        Assert.IsNotNull(stored);
        Assert.IsTrue(stored!.IsActive);
    }

    [TestMethod]
    public async Task GetTickRecords_CapturesPriorAtFirstGetAndPostAtSet()
    {
        _gateway.Seed(new AlarmState { Id = AlarmId, IsActive = false });

        var repo = CreateRepo();
        var state = await repo.GetAlarmStateAsync(AlarmId);
        state.IsActive = true;
        await repo.SetAlarmStateAsync(state);

        var records = repo.GetTickRecords().ToList();
        Assert.HasCount(1, records);
        Assert.AreEqual(AlarmId, records[0].AlarmId);
        Assert.IsFalse(records[0].Prior.IsActive);
        Assert.IsTrue(records[0].Post.IsActive);
    }

    [TestMethod]
    public async Task GetTickRecords_PriorIsFirstGet_NotMutatedByLaterMutations()
    {
        // AlarmEvaluation mutates the returned AlarmState in place before Set —
        // the repo must snapshot prior at Get time, not at Set time.
        _gateway.Seed(new AlarmState
        {
            Id = AlarmId,
            IsActive = true,
            IsAcknowledged = true,
        });

        var repo = CreateRepo();
        var state = await repo.GetAlarmStateAsync(AlarmId);
        state.IsActive = false;
        state.IsAcknowledged = false;
        await repo.SetAlarmStateAsync(state);

        var records = repo.GetTickRecords().ToList();
        Assert.IsTrue(records[0].Prior.IsActive);
        Assert.IsTrue(records[0].Prior.IsAcknowledged);
        Assert.IsFalse(records[0].Post.IsActive);
        Assert.IsFalse(records[0].Post.IsAcknowledged);
    }

    [TestMethod]
    public void SaveAlarmDefinitionAsync_Throws()
    {
        var repo = CreateRepo();
        Assert.ThrowsExactly<NotSupportedException>(() => _ = repo.SaveAlarmDefinitionAsync(Definition()));
    }
}
