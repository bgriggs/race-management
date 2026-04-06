namespace Channels.Counters;

public interface ICounterRepository
{
    public Task<int> SaveCounterParametersAsync(CounterParameters parameters);
    public Task<List<CounterParameters>> GetCounterParametersAsync();
    public Task<CounterState> GetCounterStateAsync(int counterId);
    public Task SetCounterStateAsync(CounterState counterState);
}
