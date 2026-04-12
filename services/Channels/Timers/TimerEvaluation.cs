using Channels.Logic;
using System.Globalization;

namespace Channels.Timers;

public class TimerEvaluation
{
    private readonly ITimerRepository timerRepository;
    private readonly IChannelRepository channelRepository;
    private readonly TimeProvider timeProvider;
    private readonly IStatementRepository statementRepository;
    private readonly LogicEvaluation logicEvaluation;


    public TimerEvaluation(ITimerRepository timerRepository, IChannelRepository channelRepository, IChannelDefinitionRepository channelDefinitionRepository,
        IStatementRepository statementRepository, TimeProvider? timeProvider = null)
    {
        this.timerRepository = timerRepository;
        this.channelRepository = channelRepository;
        this.statementRepository = statementRepository;

        this.timeProvider = timeProvider ?? TimeProvider.System;
        logicEvaluation = new LogicEvaluation(channelRepository, channelDefinitionRepository, statementRepository, timeProvider: this.timeProvider);
    }


    /// <summary>
    /// Evaluate all timers: detect start/stop edges, update running values, and apply limit/rollover rules.
    /// Both start and stop conditions are edge-sensitive (false→true transitions).
    /// </summary>
    public async Task UpdateTimersAsync()
    {
        var parameters = await timerRepository.GetTimersAsync();
        var now = timeProvider.GetUtcNow();

        foreach (var parameter in parameters)
        {
            var timerState = await timerRepository.GetTimerStateAsync(parameter.Id);

            // Always evaluate both conditions for edge detection, regardless of timer state.
            bool currentStart = await logicEvaluation.EvaluateAsync(parameter.StartStatementId);
            bool currentStop = await logicEvaluation.EvaluateAsync(parameter.StopStatementId);

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
                    if (parameter.EnableStartSeconds)
                    {
                        startValue = parameter.StartSeconds;
                    }
                    else
                    {
                        // Resume from current output channel value, or 0 if not set / not parseable.
                        var outputCh = await channelRepository.GetChannelValueAsync(parameter.OutputChId);
                        startValue = double.TryParse(outputCh.Value, CultureInfo.InvariantCulture, out double v) ? v : 0;
                    }

                    timerState.StartValue = startValue;
                    timerState.Started = now;

                    await SetOutputValueAsync(parameter.OutputChId, startValue);
                }
            }
            else
            {
                // Timer IS running
                if (stopEdge)
                {
                    // Stop the timer — apply stop setting or freeze at current value.
                    double stopValue;
                    if (parameter.EnableStopSeconds)
                    {
                        stopValue = parameter.StopSeconds;
                    }
                    else
                    {
                        stopValue = CalculateCurrentValue(timerState, parameter, now);
                    }

                    timerState.Started = null;
                    await SetOutputValueAsync(parameter.OutputChId, stopValue);
                }
                else
                {
                    // Timer still running — calculate current value and apply limits.
                    double currentValue = CalculateCurrentValue(timerState, parameter, now);
                    bool hitLimit = false;

                    if (parameter.RolloverSeconds > 0)
                    {
                        if (!parameter.CountDown && currentValue > parameter.RolloverSeconds)
                        {
                            if (parameter.EnableRollover)
                            {
                                // Count up rollover: wrap around using modulo.
                                currentValue %= parameter.RolloverSeconds;
                            }
                            else
                            {
                                // Count up no rollover: clamp to high limit and stop.
                                currentValue = parameter.RolloverSeconds;
                                hitLimit = true;
                            }
                        }
                        else if (parameter.CountDown && currentValue < 0)
                        {
                            if (parameter.EnableRollover)
                            {
                                // Count down rollover: wrap to high limit using positive modulo.
                                currentValue = ((currentValue % parameter.RolloverSeconds) + parameter.RolloverSeconds) % parameter.RolloverSeconds;
                            }
                            else
                            {
                                // Count down no rollover: clamp to zero and stop.
                                currentValue = 0;
                                hitLimit = true;
                            }
                        }
                    }

                    await SetOutputValueAsync(parameter.OutputChId, currentValue);

                    if (hitLimit)
                    {
                        timerState.Started = null;
                    }
                }
            }

            await timerRepository.SetTimerStateAsync(timerState);
        }
    }

    private static double CalculateCurrentValue(TimerState state, TimerParameters parameters, DateTimeOffset now)
    {
        double elapsed = (now - state.Started!.Value).TotalSeconds;
        return parameters.CountDown
            ? state.StartValue - elapsed
            : state.StartValue + elapsed;
    }

    private async Task SetOutputValueAsync(int channelId, double value)
    {
        await channelRepository.SetChannelValueAsync(new ChannelValue
        {
            Id = channelId,
            Value = value.ToString(CultureInfo.InvariantCulture),
        });
    }
}