namespace Channels.Timers;

public class TimerMemoryRepository : ITimerRepository
{
    private readonly List<TimerDefinition> timerDefinitions = [];
    private readonly Dictionary<int, TimerState> timerStates = [];

    public void AddTimer(TimerDefinition definition) => timerDefinitions.Add(definition);

    public void SetState(TimerState state) => timerStates[state.Id] = state;

    public TimerState? GetState(int id) =>
        timerStates.TryGetValue(id, out var state) ? state : null;

    public Task<IEnumerable<TimerDefinition>> GetTimerDefinitionsAsync()
    {
        return Task.FromResult(timerDefinitions.AsEnumerable());
    }

    public Task SaveTimerDefinitionsAsync(IEnumerable<TimerDefinition> definitions)
    {
        timerDefinitions.Clear();
        timerDefinitions.AddRange(definitions);
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

    public Task<IEnumerable<TimerDefinition>> GetDefinitionsAsync() =>
        GetTimerDefinitionsAsync();

    public Task SaveDefinitionsAsync(IEnumerable<TimerDefinition> definitions) =>
        SaveTimerDefinitionsAsync(definitions);

    public Task<TimerState> GetStateAsync(int id) =>
        GetTimerStateAsync(id);

    public Task SetStateAsync(TimerState state) =>
        SetTimerStateAsync(state);
}
