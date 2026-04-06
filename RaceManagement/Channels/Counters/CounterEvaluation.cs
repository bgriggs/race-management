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
        var parameters = await counterRepository.GetCounterParametersAsync();
        foreach (var parameter in parameters)
        {
            var state = await counterRepository.GetCounterStateAsync(parameter.Id);

            // Initialize on first run
            if (!state.Initialized)
            {
                state.Value = parameter.StartValue;
                state.Initialized = true;
            }

            // Read current channel values (0 means not configured)
            bool upIsNonZero = parameter.UpChId > 0
                && (await channelRepository.GetChannelValueAsync(parameter.UpChId)).GetValueDouble() != 0;
            bool downIsNonZero = parameter.DownChId > 0
                && (await channelRepository.GetChannelValueAsync(parameter.DownChId)).GetValueDouble() != 0;
            bool resetIsNonZero = parameter.ResetChId > 0
                && (await channelRepository.GetChannelValueAsync(parameter.ResetChId)).GetValueDouble() != 0;

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
                state.Value = parameter.StartValue;
            }
            else
            {
                if (upEdge)
                {
                    state.Value++;
                    if (state.Value > parameter.MaxValue)
                    {
                        state.Value = parameter.RollAtLimit ? parameter.MinValue : parameter.MaxValue;
                    }
                }

                if (downEdge)
                {
                    state.Value--;
                    if (state.Value < parameter.MinValue)
                    {
                        state.Value = parameter.RollAtLimit ? parameter.MaxValue : parameter.MinValue;
                    }
                }
            }

            await channelRepository.SetChannelValueAsync(new ChannelValue
            {
                Id = parameter.OutputChId,
                Value = state.Value.ToString(),
            });

            await counterRepository.SetCounterStateAsync(state);
        }
    }
}
