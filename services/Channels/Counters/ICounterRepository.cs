using Channels.Repositories;

namespace Channels.Counters;

public interface ICounterRepository : IMutableDefinitionSetRepository<CounterDefinition>, IStateRepository<int, CounterState>
{
    public Task<int> SaveCounterDefinitionAsync(CounterDefinition definition);
    public Task<List<CounterDefinition>> GetCounterDefinitionsAsync();
    public Task<CounterState> GetCounterStateAsync(int counterId);
    public Task SetCounterStateAsync(CounterState counterState);
}
