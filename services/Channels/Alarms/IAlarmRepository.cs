using Channels.Repositories;

namespace Channels.Alarms;

public interface IAlarmRepository : IMutableDefinitionSetRepository<AlarmDefinition>, IStateRepository<Guid, AlarmState>
{
    public Task<Guid> SaveAlarmDefinitionAsync(AlarmDefinition definition);
    public Task<List<AlarmDefinition>> GetAlarmDefinitionsAsync();
    public Task<AlarmState> GetAlarmStateAsync(Guid alarmId);
    public Task SetAlarmStateAsync(AlarmState alarmState);
}
