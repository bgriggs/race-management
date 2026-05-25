using ChannelProcessor.Alarms.State;
using Cloud.Shared;
using Cloud.Shared.Alarms;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models.Alarms;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ChannelProcessor.Alarms.Persistence;

/// <summary>
/// Reflects an evaluation tick's per-alarm pre/post snapshots into Postgres:
/// upserts the current-state <see cref="ActiveAlarmRow"/> for the UI, appends an
/// <see cref="AlarmEventRow"/> for each Activate/Deactivate edge, and publishes one
/// <see cref="AlarmChangeNotification"/> per edge to <c>alarm-changes:{teamId}</c>
/// for the WebApi SignalR fanout. Acknowledge events are emitted by the WebApi ack
/// endpoint (not visible from pre/post here because AlarmEvaluation does not toggle
/// <c>IsAcknowledged</c> true).
/// </summary>
public sealed class ActiveAlarmStore(
    IDbContextFactory<RaceManagementContext> dbFactory,
    IConnectionMultiplexer redis,
    ILogger<ActiveAlarmStore> logger)
{
    public async Task RecordTickAsync(
        int teamId,
        string carNumber,
        IReadOnlyList<RedisAlarmRepository.TickRecord> records,
        DateTime tickTimestampUtc,
        CancellationToken ct)
    {
        if (records.Count == 0) return;

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var ids = records.Select(r => r.AlarmId).ToArray();
        var existing = await db.ActiveAlarms
            .Where(a => a.TeamId == teamId && a.CarNumber == carNumber && ids.Contains(a.AlarmDefinitionId))
            .ToDictionaryAsync(a => a.AlarmDefinitionId, ct);

        var edges = new List<AlarmChangeNotification>();

        foreach (var r in records)
        {
            var post = r.Post;
            var prior = r.Prior;

            if (!existing.TryGetValue(r.AlarmId, out var row))
            {
                row = new ActiveAlarmRow
                {
                    TeamId = teamId,
                    CarNumber = carNumber,
                    AlarmDefinitionId = r.AlarmId,
                    IsActive = post.IsActive,
                    IsAcknowledged = post.IsAcknowledged,
                    LastActivatedAt = post.IsActive ? tickTimestampUtc : null,
                    LastAcknowledgedTimestamp = post.LastAcknowledgedTimestamp == default ? null : post.LastAcknowledgedTimestamp,
                };
                db.ActiveAlarms.Add(row);
            }
            else
            {
                row.IsActive = post.IsActive;
                row.IsAcknowledged = post.IsAcknowledged;
                row.LastAcknowledgedTimestamp = post.LastAcknowledgedTimestamp == default ? null : post.LastAcknowledgedTimestamp;
                if (!prior.IsActive && post.IsActive) row.LastActivatedAt = tickTimestampUtc;
            }

            if (!prior.IsActive && post.IsActive)
            {
                db.AlarmEvents.Add(new AlarmEventRow
                {
                    TeamId = teamId,
                    CarNumber = carNumber,
                    AlarmDefinitionId = r.AlarmId,
                    EventType = AlarmEventType.Activated,
                    Timestamp = tickTimestampUtc,
                });
                edges.Add(BuildNotification(teamId, carNumber, r.AlarmId, AlarmEventType.Activated, post, tickTimestampUtc));
            }
            else if (prior.IsActive && !post.IsActive)
            {
                db.AlarmEvents.Add(new AlarmEventRow
                {
                    TeamId = teamId,
                    CarNumber = carNumber,
                    AlarmDefinitionId = r.AlarmId,
                    EventType = AlarmEventType.Deactivated,
                    Timestamp = tickTimestampUtc,
                });
                edges.Add(BuildNotification(teamId, carNumber, r.AlarmId, AlarmEventType.Deactivated, post, tickTimestampUtc));
            }
        }

        await db.SaveChangesAsync(ct);

        // Publish AFTER Postgres writes so subscribers never see a notification for a row
        // that doesn't exist yet. Matches the ADR-0002 ACK ordering rule.
        if (edges.Count > 0)
        {
            var sub = redis.GetSubscriber();
            var channel = RedisChannel.Literal(string.Format(Consts.ALARM_CHANGES_CHANNEL, teamId));
            foreach (var edge in edges)
            {
                await sub.PublishAsync(channel, MessagePackSerializer.Serialize(edge, cancellationToken: ct));
            }
            logger.LogDebug("Alarm tick for team {TeamId} car {CarNumber}: {Count} edge notifications published",
                teamId, carNumber, edges.Count);
        }
    }

    private static AlarmChangeNotification BuildNotification(
        int teamId, string carNumber, Guid alarmId, AlarmEventType eventType,
        RedisAlarmRepository.StateSnapshot post, DateTime timestampUtc) =>
        new()
        {
            TeamId = teamId,
            CarNumber = carNumber,
            AlarmDefinitionId = alarmId,
            EventType = eventType,
            IsActive = post.IsActive,
            IsAcknowledged = post.IsAcknowledged,
            Timestamp = timestampUtc,
        };
}
