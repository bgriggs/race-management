using Channels.Logic;

namespace Channels.Alarms;

/// <summary>
/// Runs the evaluation of alarm statements and updates alarm states accordingly. This includes edge detection for activating/deactivating alarms, managing acknowledgment state and timing, and writing to optional status channels.
/// </summary>
public class AlarmEvaluation
{
    private readonly IAlarmRepository alarmRepository;
    private readonly IChannelRepository channelRepository;
    private readonly TimeProvider timeProvider;
    private readonly LogicEvaluation logicEvaluation;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlarmEvaluation"/> class.
    /// </summary>
    public AlarmEvaluation(
        IAlarmRepository alarmRepository,
        IChannelRepository channelRepository,
        IChannelDefinitionRepository channelDefinitionRepository,
        TimeProvider? timeProvider = null)
    {
        this.alarmRepository = alarmRepository;
        this.channelRepository = channelRepository;
        this.timeProvider = timeProvider ?? TimeProvider.System;

        logicEvaluation = new LogicEvaluation(channelRepository, channelDefinitionRepository, timeProvider: this.timeProvider);
    }

    /// <summary>
    /// Checks the current state of all alarm definitions, evaluates their statements, and updates their active/acknowledged states accordingly. 
    /// Also handles writing to alarm status channels if configured. This method should be called periodically to ensure alarms are evaluated in a timely manner.
    /// </summary>
    public async Task UpdateAlarmsAsync()
    {
        var alarmDefinitions = await alarmRepository.GetAlarmDefinitionsAsync();
        var now = timeProvider.GetUtcNow();

        foreach (var alarmDefinition in alarmDefinitions)
        {
            var alarmState = await alarmRepository.GetAlarmStateAsync(alarmDefinition.Id);

            bool shouldBeActive = await logicEvaluation.EvaluateAsync(alarmDefinition.Statement);

            if (shouldBeActive && !alarmState.IsActive)
            {
                alarmState.IsAcknowledged = false;
                alarmState.LastAcknowledgedTimestamp = default;
            }

            alarmState.IsActive = shouldBeActive;

            if (!alarmState.IsActive)
            {
                alarmState.IsAcknowledged = false;
                alarmState.LastAcknowledgedTimestamp = default;
            }
            else if (alarmState.IsAcknowledged
                && alarmState.LastAcknowledgedTimestamp != default
                && now >= alarmState.LastAcknowledgedTimestamp.AddSeconds(alarmDefinition.TimeAfterAckToDisplaySecs))
            {
                alarmState.IsAcknowledged = false;
                alarmState.LastAcknowledgedTimestamp = default;
            }

            await alarmRepository.SetAlarmStateAsync(alarmState);

            if (alarmDefinition.AlarmStatusChannelId is Guid alarmStatusChannelId && alarmStatusChannelId != Guid.Empty)
            {
                await channelRepository.SetChannelValueAsync(alarmStatusChannelId, new ChannelValue
                {
                    Value = alarmState.IsActive ? "1" : "0",
                });
            }
        }
    }

}
