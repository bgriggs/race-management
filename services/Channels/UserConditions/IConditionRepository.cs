using Channels.Repositories;

namespace Channels.UserConditions;

public interface IConditionRepository : IMutableDefinitionSetRepository<ConditionDefinition>, IStateRepository<int, ConditionState>
{
    public Task<IEnumerable<ConditionDefinition>> GetConditionDefinitionsAsync();
    public Task SaveConditionDefinitionsAsync(IEnumerable<ConditionDefinition> conditions);
    public Task<ConditionState> GetConditionStateAsync(int conditionId);
    public Task SetConditionStateAsync(ConditionState conditionState);
}
