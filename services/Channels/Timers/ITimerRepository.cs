using Channels.Repositories;

namespace Channels.Timers;

public interface ITimerRepository : IMutableDefinitionSetRepository<TimerDefinition>, IStateRepository<int, TimerState>
{
    public Task<IEnumerable<TimerDefinition>> GetTimerDefinitionsAsync();
    public Task SaveTimerDefinitionsAsync(IEnumerable<TimerDefinition> definitions);
    public Task<TimerState> GetTimerStateAsync(int timerId);
    public Task SetTimerStateAsync(TimerState timerState);
}
