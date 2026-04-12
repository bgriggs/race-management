namespace Channels.Counters;

public class CounterMemoryRepository : ICounterRepository
{
    private readonly List<CounterDefinition> definitions = [];
    private readonly Dictionary<Guid, CounterState> states = [];

    public void Add(CounterDefinition definition) => definitions.Add(definition);

    public void SetState(CounterState state) => states[state.Id] = state;

    public CounterState? GetState(Guid id) =>
        states.TryGetValue(id, out var state) ? state : null;

    public Task<Guid> SaveCounterDefinitionAsync(CounterDefinition definition)
    {
        definitions.Add(definition);
        return Task.FromResult(definition.Id);
    }

    public Task<List<CounterDefinition>> GetCounterDefinitionsAsync() =>
        Task.FromResult(new List<CounterDefinition>(definitions));

    public Task<CounterState> GetCounterStateAsync(Guid counterId)
    {
        states.TryGetValue(counterId, out var state);
        state ??= new CounterState { Id = counterId };
        return Task.FromResult(state);
    }

    public Task SetCounterStateAsync(CounterState counterState)
    {
        states[counterState.Id] = counterState;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<CounterDefinition>> GetDefinitionsAsync() =>
        Task.FromResult(definitions.AsEnumerable());

    public Task SaveDefinitionsAsync(IEnumerable<CounterDefinition> items)
    {
        definitions.Clear();
        definitions.AddRange(items);
        return Task.CompletedTask;
    }

    public Task<CounterState> GetStateAsync(Guid id) =>
        GetCounterStateAsync(id);

    public Task SetStateAsync(CounterState state) =>
        SetCounterStateAsync(state);
}
