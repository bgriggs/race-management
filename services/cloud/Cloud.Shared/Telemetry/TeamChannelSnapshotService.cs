using Cloud.Shared.Database;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Cloud.Shared.Telemetry;

/// <summary>
/// Builds a full channel-state snapshot for every car belonging to a team by
/// reading the per-car Redis hashes maintained by ChannelProcessor.
/// </summary>
public interface ITeamChannelSnapshotService
{
    Task<CarChannelSnapshot[]> GetTeamSnapshotAsync(int teamId, CancellationToken ct = default);
}

public class TeamChannelSnapshotService(
    IConnectionMultiplexer redis,
    IDbContextFactory<RaceManagementContext> dbFactory,
    ILogger<TeamChannelSnapshotService> logger) : ITeamChannelSnapshotService
{
    public async Task<CarChannelSnapshot[]> GetTeamSnapshotAsync(int teamId, CancellationToken ct = default)
    {
        await using var context = await dbFactory.CreateDbContextAsync(ct);
        var carNumbers = await context.Cars
            .AsNoTracking()
            .Where(c => c.TeamId == teamId && !c.IsDeleted)
            .Select(c => c.Number)
            .ToListAsync(ct);

        if (carNumbers.Count == 0)
            return [];

        var db = redis.GetDatabase();
        var results = new CarChannelSnapshot[carNumbers.Count];
        for (int i = 0; i < carNumbers.Count; i++)
        {
            var carNumber = carNumbers[i];
            var carKey = string.Format(Consts.CAR_STREAM_FIELD, teamId, carNumber);
            var hashKey = new RedisKey(string.Format(Consts.CAR_CHANNEL_STATE_KEY, carKey));
            var activeConfigKey = new RedisKey(string.Format(Consts.CAR_ACTIVE_CONFIG_KEY, carKey));

            var entries = await db.HashGetAllAsync(hashKey);
            var channels = new Dictionary<ushort, ChannelValueSnapshot>(entries.Length);
            foreach (var entry in entries)
            {
                if (!ushort.TryParse(entry.Name.ToString(), out var index)) continue;
                try
                {
                    channels[index] = MessagePackSerializer.Deserialize<ChannelValueSnapshot>((byte[])entry.Value!, cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to deserialize ChannelValueSnapshot for car {CarKey} channel {Index}", carKey, index);
                }
            }

            Guid? configurationId = null;
            var configIdValue = await db.StringGetAsync(activeConfigKey);
            if (configIdValue.HasValue && Guid.TryParse(configIdValue.ToString(), out var parsed))
            {
                configurationId = parsed;
            }

            results[i] = new CarChannelSnapshot
            {
                CarKey = carKey,
                CarNumber = carNumber,
                Channels = channels,
                ConfigurationId = configurationId,
            };
        }

        return results;
    }
}
