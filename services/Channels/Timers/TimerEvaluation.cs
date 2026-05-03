using Channels.Logic;
using System.Globalization;

namespace Channels.Timers;

public class TimerEvaluation
{
    private readonly ITimerRepository timerRepository;
    private readonly IChannelRepository channelRepository;
    private readonly TimeProvider timeProvider;
    private readonly LogicEvaluation logicEvaluation;


    public TimerEvaluation(ITimerRepository timerRepository, IChannelRepository channelRepository, IChannelDefinitionRepository channelDefinitionRepository,
        TimeProvider? timeProvider = null)
    {
        this.timerRepository = timerRepository;
        this.channelRepository = channelRepository;

        this.timeProvider = timeProvider ?? TimeProvider.System;
        logicEvaluation = new LogicEvaluation(channelRepository, channelDefinitionRepository, timeProvider: this.timeProvider);
    }


    /// <summary>
    /// Evaluate all timers: detect start/stop edges, update running values, and apply limit/rollover rules.
    /// Both start and stop conditions are edge-sensitive (false→true transitions).
    /// </summary>
    public async Task UpdateTimersAsync()
    {
        var definitions = await timerRepository.GetTimerDefinitionsAsync();
        var now = timeProvider.GetUtcNow();

        foreach (var definition in definitions)
        {
            var timerState = await timerRepository.GetTimerStateAsync(definition.Id);

            // Evaluate start condition from ActivateComparisons and stop condition from DeactivateComparisons.
            // Each is edge-sensitive. Comparison groups are evaluated directly since TimerEvaluation
            // manages its own edge state (PreviousStartResult/PreviousStopResult).
            //
            // When DeactivateComparisons is null, ActivateComparisons drives both:
            //   start = false→true on ActivateComparisons
            //   stop  = true→false on ActivateComparisons (i.e. !currentStart)
            bool currentStart = await logicEvaluation.EvaluateComparisonsAsync(definition.Statement.ActivateComparisons);

            bool currentStop;
            if (definition.Statement.DeactivateComparisons is { Count: > 0 })
            {
                currentStop = await logicEvaluation.EvaluateComparisonsAsync(definition.Statement.DeactivateComparisons);
            }
            else
            {
                currentStop = !currentStart;
            }

            // Detect edges: false → true transitions
            bool startEdge = !timerState.PreviousStartResult && currentStart;
            bool stopEdge = !timerState.PreviousStopResult && currentStop;

            // Update edge tracking for next cycle
            timerState.PreviousStartResult = currentStart;
            timerState.PreviousStopResult = currentStop;

            if (timerState.Started is null)
            {
                // Timer is NOT running — only a start edge can transition it to running.
                if (startEdge)
                {
                    double startValue;
                    if (definition.EnableStartSeconds)
                    {
                        startValue = definition.StartSeconds;
                    }
                    else
                    {
                        // Resume from current output channel value, or 0 if not set / not parseable.
                        var outputCh = await channelRepository.GetChannelValueAsync(definition.OutputChId);
                        startValue = double.TryParse(outputCh.Value, CultureInfo.InvariantCulture, out double v) ? v : 0;
                    }

                    timerState.StartValue = startValue;
                    timerState.Started = now;

                    await SetOutputValueAsync(definition.OutputChId, startValue);
                }
            }
            else
            {
                // Timer IS running
                if (stopEdge)
                {
                    // Stop the timer — apply stop setting or freeze at current value.
                    double stopValue;
                    if (definition.EnableStopSeconds)
                    {
                        stopValue = definition.StopSeconds;
                    }
                    else
                    {
                        stopValue = CalculateCurrentValue(timerState, definition, now);
                    }

                    timerState.Started = null;
                    await SetOutputValueAsync(definition.OutputChId, stopValue);
                }
                else
                {
                    // Timer still running — calculate current value and apply limits.
                    double currentValue = CalculateCurrentValue(timerState, definition, now);
                    bool hitLimit = false;

                    if (definition.RolloverSeconds > 0)
                    {
                        if (!definition.CountDown && currentValue > definition.RolloverSeconds)
                        {
                            if (definition.EnableRollover)
                            {
                                // Count up rollover: wrap around using modulo.
                                currentValue %= definition.RolloverSeconds;
                            }
                            else
                            {
                                // Count up no rollover: clamp to high limit and stop.
                                currentValue = definition.RolloverSeconds;
                                hitLimit = true;
                            }
                        }
                        else if (definition.CountDown && currentValue < 0)
                        {
                            if (definition.EnableRollover)
                            {
                                // Count down rollover: wrap to high limit using positive modulo.
                                currentValue = ((currentValue % definition.RolloverSeconds) + definition.RolloverSeconds) % definition.RolloverSeconds;
                            }
                            else
                            {
                                // Count down no rollover: clamp to zero and stop.
                                currentValue = 0;
                                hitLimit = true;
                            }
                        }
                    }

                    await SetOutputValueAsync(definition.OutputChId, currentValue);

                    if (hitLimit)
                    {
                        timerState.Started = null;
                    }
                }
            }

            await timerRepository.SetTimerStateAsync(timerState);
        }
    }

    private static double CalculateCurrentValue(TimerState state, TimerDefinition definition, DateTimeOffset now)
    {
        double elapsed = (now - state.Started!.Value).TotalSeconds;
        return definition.CountDown
            ? state.StartValue - elapsed
            : state.StartValue + elapsed;
    }

    private async Task SetOutputValueAsync(Guid channelId, double value)
    {
        await channelRepository.SetChannelValueAsync(channelId, new ChannelValue
        {
            Value = value.ToString(CultureInfo.InvariantCulture),
        });
    }
}
