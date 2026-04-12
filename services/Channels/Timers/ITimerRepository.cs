namespace Channels.Timers;

public interface ITimerRepository
{
    public Task<IEnumerable<TimerDefinition>> GetTimerDefinitionsAsync();
    public Task SaveTimerDefinitionsAsync(IEnumerable<TimerDefinition> definitions);
    public Task<TimerState> GetTimerStateAsync(int timerId);
    public Task SetTimerStateAsync(TimerState timerState);
}
