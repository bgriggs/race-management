namespace Channels.UserConditions;

public interface IConditionRepository
{
    public Task<IEnumerable<ConditionDefinition>> GetConditionDefinitionsAsync();
    public Task SaveConditionDefinitionsAsync(IEnumerable<ConditionDefinition> conditions);
    public Task<ConditionState> GetConditionStateAsync(int conditionId);
    public Task SetConditionStateAsync(ConditionState conditionState);
}
