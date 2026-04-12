namespace Channels.Alarms;

public class AlarmMemoryRepository : IAlarmRepository
{
    private readonly List<AlarmDefinition> definitions = [];
    private readonly Dictionary<Guid, AlarmState> states = [];

    public void Add(AlarmDefinition definition) => definitions.Add(definition);

    public void SetState(AlarmState state) => states[state.Id] = state;

    public AlarmState? GetState(Guid id) =>
        states.TryGetValue(id, out var state) ? state : null;

    public Task<Guid> SaveAlarmDefinitionAsync(AlarmDefinition definition)
    {
        definitions.Add(definition);
        return Task.FromResult(definition.Id);
    }

    public Task<List<AlarmDefinition>> GetAlarmDefinitionsAsync() =>
        Task.FromResult(new List<AlarmDefinition>(definitions));

    public Task<AlarmState> GetAlarmStateAsync(Guid alarmId)
    {
        states.TryGetValue(alarmId, out var state);
        state ??= new AlarmState { Id = alarmId };
        return Task.FromResult(state);
    }

    public Task SetAlarmStateAsync(AlarmState alarmState)
    {
        states[alarmState.Id] = alarmState;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<AlarmDefinition>> GetDefinitionsAsync() =>
        Task.FromResult(definitions.AsEnumerable());

    public Task SaveDefinitionsAsync(IEnumerable<AlarmDefinition> items)
    {
        definitions.Clear();
        definitions.AddRange(items);
        return Task.CompletedTask;
    }

    public Task<AlarmState> GetStateAsync(Guid id) =>
        GetAlarmStateAsync(id);

    public Task SetStateAsync(AlarmState state) =>
        SetAlarmStateAsync(state);
}
