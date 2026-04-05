using System;
using System.Collections.Generic;
using System.Text;

namespace Channels.Timers;

public class TimerMemoryRepository : ITimerRepository
{
    private readonly List<TimerParameters> timerParameters = [];
    private readonly Dictionary<int, TimerState> timerStates = [];

    public Task<IEnumerable<TimerParameters>> GetTimersAsync()
    {
        return Task.FromResult(timerParameters.AsEnumerable());
    }

    public Task SaveTimersAsync(IEnumerable<TimerParameters> timers)
    {
        timerParameters.Clear();
        timerParameters.AddRange(timers);
        return Task.CompletedTask;
    }

    public Task<TimerState> GetTimerStateAsync(int timerId)
    {
        _ = timerStates.TryGetValue(timerId, out TimerState? state);
        state ??= new TimerState { Id = timerId };
        return Task.FromResult(state);
    }

    public Task SetTimerStateAsync(TimerState timerState)
    {
        timerStates[timerState.Id] = timerState;
        return Task.CompletedTask;
    }
}
