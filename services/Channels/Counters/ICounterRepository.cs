namespace Channels.Counters;

public interface ICounterRepository
{
    public Task<int> SaveCounterDefinitionAsync(CounterDefinition definition);
    public Task<List<CounterDefinition>> GetCounterDefinitionsAsync();
    public Task<CounterState> GetCounterStateAsync(int counterId);
    public Task SetCounterStateAsync(CounterState counterState);
}
