using Channels.Alarms;
using Cloud.Shared.Alarms;

namespace ChannelProcessor.Alarms.State;

/// <summary>
/// Per-car, per-evaluation <see cref="IAlarmRepository"/>. Delegates Redis Get/Set to the
/// shared <see cref="IRedisAlarmStateGateway"/> so the JSON shape and key format are
/// authoritative in one place; layers the prior-vs-post snapshot capture on top so
/// <c>ActiveAlarmStore</c> can persist edge transitions without re-reading. Definitions
/// are passed in pre-resolved; persistence of definitions lives in
/// <c>AlarmDefinitionRepository</c> (Postgres).
/// </summary>
public sealed class RedisAlarmRepository(
    IRedisAlarmStateGateway gateway,
    string carKey,
    IReadOnlyList<AlarmDefinition> definitions) : IAlarmRepository
{
    public readonly record struct StateSnapshot(bool IsActive, bool IsAcknowledged, DateTime LastAcknowledgedTimestamp);
    public readonly record struct TickRecord(Guid AlarmId, StateSnapshot Prior, StateSnapshot Post);

    private readonly Dictionary<Guid, StateSnapshot> _prior = new();
    private readonly Dictionary<Guid, StateSnapshot> _post = new();

    public Task<List<AlarmDefinition>> GetAlarmDefinitionsAsync()
        => Task.FromResult(new List<AlarmDefinition>(definitions));

    public async Task<AlarmState> GetAlarmStateAsync(Guid alarmId)
    {
        var state = await gateway.GetAsync(carKey, alarmId);

        // First Get for this alarm in this evaluation is the "prior" snapshot.
        _prior.TryAdd(alarmId, Snapshot(state));
        return state;
    }

    public async Task SetAlarmStateAsync(AlarmState alarmState)
    {
        await gateway.SetAsync(carKey, alarmState);
        _post[alarmState.Id] = Snapshot(alarmState);
    }

    public IEnumerable<TickRecord> GetTickRecords()
    {
        foreach (var (id, post) in _post)
        {
            var prior = _prior.GetValueOrDefault(id);
            yield return new TickRecord(id, prior, post);
        }
    }

    public Task<Guid> SaveAlarmDefinitionAsync(AlarmDefinition definition)
        => throw new NotSupportedException("Alarm definition persistence is owned by AlarmDefinitionRepository (Postgres).");

    public Task<IEnumerable<AlarmDefinition>> GetDefinitionsAsync()
        => Task.FromResult(definitions.AsEnumerable());

    public Task SaveDefinitionsAsync(IEnumerable<AlarmDefinition> items)
        => throw new NotSupportedException("Alarm definition persistence is owned by AlarmDefinitionRepository (Postgres).");

    public Task<AlarmState> GetStateAsync(Guid id) => GetAlarmStateAsync(id);

    public Task SetStateAsync(AlarmState state) => SetAlarmStateAsync(state);

    private static StateSnapshot Snapshot(AlarmState state) =>
        new(state.IsActive, state.IsAcknowledged, state.LastAcknowledgedTimestamp);
}
