using Channels.Repositories;

namespace Channels.Counters;

public interface ICounterRepository : IMutableDefinitionSetRepository<CounterDefinition>, IStateRepository<Guid, CounterState>
{
    public Task<Guid> SaveCounterDefinitionAsync(CounterDefinition definition);
    public Task<List<CounterDefinition>> GetCounterDefinitionsAsync();
    public Task<CounterState> GetCounterStateAsync(Guid counterId);
    public Task SetCounterStateAsync(CounterState counterState);
}
