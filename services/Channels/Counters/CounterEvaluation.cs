namespace Channels.Counters;

public class CounterEvaluation
{
    private readonly ICounterRepository counterRepository;
    private readonly IChannelRepository channelRepository;

    public CounterEvaluation(ICounterRepository counterRepository, IChannelRepository channelRepository)
    {
        this.counterRepository = counterRepository;
        this.channelRepository = channelRepository;
    }

    /// <summary>
    /// Evaluate all counters: detect rising edges on up/down/reset channels and update counter values.
    /// Rising edge means the channel value transitions from 0 to non-zero.
    /// </summary>
    public async Task UpdateCountersAsync()
    {
        var definitions = await counterRepository.GetCounterDefinitionsAsync();
        foreach (var definition in definitions)
        {
            var state = await counterRepository.GetCounterStateAsync(definition.Id);

            // Initialize on first run
            if (!state.Initialized)
            {
                state.Value = definition.StartValue;
                state.Initialized = true;
            }

            // Read current channel values (0 means not configured)
            bool upIsNonZero = definition.UpChId > 0
                && (await channelRepository.GetChannelValueAsync(definition.UpChId)).GetValueDouble() != 0;
            bool downIsNonZero = definition.DownChId > 0
                && (await channelRepository.GetChannelValueAsync(definition.DownChId)).GetValueDouble() != 0;
            bool resetIsNonZero = definition.ResetChId > 0
                && (await channelRepository.GetChannelValueAsync(definition.ResetChId)).GetValueDouble() != 0;

            // Detect rising edges: previous was zero AND current is non-zero
            bool upEdge = state.PreviousUpWasZero && upIsNonZero;
            bool downEdge = state.PreviousDownWasZero && downIsNonZero;
            bool resetEdge = state.PreviousResetWasZero && resetIsNonZero;

            // Update edge tracking for next cycle
            state.PreviousUpWasZero = !upIsNonZero;
            state.PreviousDownWasZero = !downIsNonZero;
            state.PreviousResetWasZero = !resetIsNonZero;

            // Reset takes priority over increment/decrement
            if (resetEdge)
            {
                state.Value = definition.StartValue;
            }
            else
            {
                if (upEdge)
                {
                    state.Value++;
                    if (state.Value > definition.MaxValue)
                    {
                        state.Value = definition.RollAtLimit ? definition.MinValue : definition.MaxValue;
                    }
                }

                if (downEdge)
                {
                    state.Value--;
                    if (state.Value < definition.MinValue)
                    {
                        state.Value = definition.RollAtLimit ? definition.MaxValue : definition.MinValue;
                    }
                }
            }

            await channelRepository.SetChannelValueAsync(new ChannelValue
            {
                Id = definition.OutputChId,
                Value = state.Value.ToString(),
            });

            await counterRepository.SetCounterStateAsync(state);
        }
    }
}
