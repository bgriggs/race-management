using Cloud.Shared;
using Cloud.Shared.Telemetry;
using MessagePack;
using StackExchange.Redis;

namespace ChannelProcessor.Telemetry;

public class TeamChannelStateRepository(IConnectionMultiplexer redis, ILogger<TeamChannelStateRepository> logger) : ITeamChannelStateRepository
{
    public async Task<bool> SetIfChangedAsync(int teamId, Guid channelId, ChannelValueSnapshot incoming, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var hashKey = new RedisKey(string.Format(Consts.TEAM_CHANNEL_STATE_KEY, teamId));
        var field = new RedisValue(channelId.ToString());

        var existing = await db.HashGetAsync(hashKey, field);
        if (existing.HasValue)
        {
            var stored = MessagePackSerializer.Deserialize<ChannelValueSnapshot>((byte[])existing!, cancellationToken: ct);
            if (stored.Value == incoming.Value)
                return false;
        }

        await db.HashSetAsync(hashKey, field, MessagePackSerializer.Serialize(incoming, cancellationToken: ct));

        var changeChannel = RedisChannel.Literal(string.Format(Consts.TEAM_CHANNEL_CHANGES_CHANNEL, teamId));
        var changePayload = MessagePackSerializer.Serialize(
            new TeamChannelChangeNotification { ChannelId = channelId, Value = incoming.Value, Timestamp = incoming.Timestamp },
            cancellationToken: ct);
        await db.PublishAsync(changeChannel, changePayload);

        logger.LogDebug("Team {TeamId} channel {ChannelId} updated to {Value}", teamId, channelId, incoming.Value);
        return true;
    }

    public async Task<Dictionary<Guid, ChannelValueSnapshot>> GetAllAsync(int teamId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var hashKey = new RedisKey(string.Format(Consts.TEAM_CHANNEL_STATE_KEY, teamId));
        var entries = await db.HashGetAllAsync(hashKey);

        var result = new Dictionary<Guid, ChannelValueSnapshot>(entries.Length);
        foreach (var entry in entries)
        {
            if (!Guid.TryParse(entry.Name.ToString(), out var channelId)) continue;
            var snapshot = MessagePackSerializer.Deserialize<ChannelValueSnapshot>((byte[])entry.Value!, cancellationToken: ct);
            result[channelId] = snapshot;
        }
        return result;
    }
}
