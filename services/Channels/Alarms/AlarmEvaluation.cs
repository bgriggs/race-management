using Channels.Logic;

namespace Channels.Alarms;

public class AlarmEvaluation
{
    private readonly IAlarmRepository alarmRepository;
    private readonly IChannelRepository channelRepository;
    private readonly TimeProvider timeProvider;
    private readonly LogicEvaluation logicEvaluation;

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

    public async Task UpdateAlarmsAsync()
    {
        var alarmDefinitions = await alarmRepository.GetAlarmDefinitionsAsync();
        var now = timeProvider.GetUtcNow();

        foreach (var alarmDefinition in alarmDefinitions)
        {
            var alarmState = await alarmRepository.GetAlarmStateAsync(alarmDefinition.Id);

            bool shouldBeActive = await EvaluateStatementsAsync(alarmDefinition.Statements);

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

    private async Task<bool> EvaluateStatementsAsync(List<StatementDefinition> statements)
    {
        if (statements.Count == 0)
            return false;

        foreach (var statementDefinition in statements)
        {
            if (!await logicEvaluation.EvaluateAsync(statementDefinition))
            {
                return false;
            }
        }

        return true;
    }
}
