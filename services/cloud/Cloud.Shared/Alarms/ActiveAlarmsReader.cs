using Cloud.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Cloud.Shared.Alarms;

public sealed class ActiveAlarmsReader(IDbContextFactory<RaceManagementContext> dbFactory) : IActiveAlarmsReader
{
    public async Task<ActiveAlarmDto[]> GetForTeamAsync(int teamId, bool includeAcknowledged, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var query =
            from a in db.ActiveAlarms.AsNoTracking()
            join d in db.AlarmDefinitions.AsNoTracking() on a.AlarmDefinitionId equals d.Id
            where a.TeamId == teamId && a.IsActive && (includeAcknowledged || !a.IsAcknowledged)
            orderby a.LastActivatedAt descending
            select new ActiveAlarmDto
            {
                TeamId = a.TeamId,
                CarNumber = a.CarNumber,
                AlarmDefinitionId = a.AlarmDefinitionId,
                Name = d.Name,
                Message = d.Message,
                DisplayChannelSourceColorHex = d.DisplayChannelSourceColorHex,
                TimeAfterAckToDisplaySecs = d.TimeAfterAckToDisplaySecs,
                IsActive = a.IsActive,
                IsAcknowledged = a.IsAcknowledged,
                LastActivatedAt = a.LastActivatedAt,
                LastAcknowledgedTimestamp = a.LastAcknowledgedTimestamp,
            };

        return await query.ToArrayAsync(ct);
    }
}
