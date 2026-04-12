using Channels.Logic;

namespace Channels.UserConditions;

public class ConditionEvaluation
{
    private readonly IConditionRepository conditionRepository;
    private readonly IChannelRepository channelRepository;
    private readonly TimeProvider timeProvider;
    private readonly IStatementRepository statementRepository;
    private readonly LogicEvaluation logicEvaluation;


    public ConditionEvaluation(IConditionRepository conditionRepository, IChannelRepository channelRepository, IChannelDefinitionRepository channelDefinitionRepository,
        IStatementRepository statementRepository, TimeProvider? timeProvider = null)
    {
        this.conditionRepository = conditionRepository;
        this.channelRepository = channelRepository;
        this.statementRepository = statementRepository;

        this.timeProvider = timeProvider ?? TimeProvider.System;
        logicEvaluation = new LogicEvaluation(channelRepository, channelDefinitionRepository, statementRepository, timeProvider: this.timeProvider);
    }

    public async Task UpdateAsync()
    {
        var conditionDefinitions = await conditionRepository.GetConditionDefinitionsAsync();
        foreach (var conditionDefinition in conditionDefinitions)
        {
            // All statements must evaluate to true for the condition to be true (AND logic across statements)
            bool result = true;
            foreach (var statementDefinition in conditionDefinition.Statements)
            {
                if (!await logicEvaluation.EvaluateAsync(statementDefinition.Id))
                {
                    result = false;
                    break;
                }
            }

            var state = await conditionRepository.GetConditionStateAsync(conditionDefinition.Id);
            state.ConditionId = conditionDefinition.Id;
            state.IsTrue = result;
            await conditionRepository.SetConditionStateAsync(state);

            if (conditionDefinition.OutputChannelId != Guid.Empty)
            {
                await channelRepository.SetChannelValueAsync(conditionDefinition.OutputChannelId, new ChannelValue
                {
                    Value = result ? "1" : "0",
                });
            }
        }
    }
}
