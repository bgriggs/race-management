using Channels.Repositories;

namespace Channels.UserConditions;

public interface IConditionRepository : IMutableDefinitionSetRepository<ConditionDefinition>, IStateRepository<Guid, ConditionState>
{
    public Task<IEnumerable<ConditionDefinition>> GetConditionDefinitionsAsync();
    public Task SaveConditionDefinitionsAsync(IEnumerable<ConditionDefinition> conditions);
    public Task<ConditionState> GetConditionStateAsync(Guid conditionId);
    public Task SetConditionStateAsync(ConditionState conditionState);
}
