namespace Channels.UserConditions;

public class ConditionMemoryRepository : IConditionRepository
{
    private readonly List<ConditionDefinition> definitions = [];
    private readonly Dictionary<int, ConditionState> states = [];

    public void Add(ConditionDefinition definition) => definitions.Add(definition);

    public ConditionState? GetState(int id) =>
        states.TryGetValue(id, out var state) ? state : null;

    public Task<IEnumerable<ConditionDefinition>> GetConditionDefinitionsAsync() =>
        Task.FromResult(definitions.AsEnumerable());

    public Task SaveConditionDefinitionsAsync(IEnumerable<ConditionDefinition> conditions)
    {
        definitions.Clear();
        definitions.AddRange(conditions);
        return Task.CompletedTask;
    }

    public Task<ConditionState> GetConditionStateAsync(int conditionId)
    {
        states.TryGetValue(conditionId, out var state);
        state ??= new ConditionState { ConditionId = conditionId };
        return Task.FromResult(state);
    }

    public Task SetConditionStateAsync(ConditionState conditionState)
    {
        states[conditionState.ConditionId] = conditionState;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<ConditionDefinition>> GetDefinitionsAsync() =>
        GetConditionDefinitionsAsync();

    public Task SaveDefinitionsAsync(IEnumerable<ConditionDefinition> definitions) =>
        SaveConditionDefinitionsAsync(definitions);

    public Task<ConditionState> GetStateAsync(int id) =>
        GetConditionStateAsync(id);

    public Task SetStateAsync(ConditionState state) =>
        SetConditionStateAsync(state);
}
