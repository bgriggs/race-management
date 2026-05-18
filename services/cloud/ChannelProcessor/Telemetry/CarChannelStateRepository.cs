using Cloud.Shared;
using Cloud.Shared.Telemetry;
using MessagePack;
using StackExchange.Redis;

namespace ChannelProcessor.Telemetry;

public class CarChannelStateRepository(IConnectionMultiplexer redis, ILogger<CarChannelStateRepository> logger) : ICarChannelStateRepository
{
    public async Task<bool> SetIfChangedAsync(string carKey, ushort sessionIndex, ChannelValueSnapshot incoming, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var hashKey = new RedisKey(string.Format(Consts.CAR_CHANNEL_STATE_KEY, carKey));
        var field = new RedisValue(sessionIndex.ToString());

        var existing = await db.HashGetAsync(hashKey, field);
        if (existing.HasValue)
        {
            var stored = MessagePackSerializer.Deserialize<ChannelValueSnapshot>((byte[])existing!, cancellationToken: ct);
            if (stored.Value == incoming.Value)
                return false;
        }

        await db.HashSetAsync(hashKey, field, MessagePackSerializer.Serialize(incoming, cancellationToken: ct));

        var changeChannel = RedisChannel.Literal(string.Format(Consts.CAR_CHANNEL_CHANGES_CHANNEL, carKey));
        var changePayload = MessagePackSerializer.Serialize(new ChannelChangeNotification { SessionIndex = sessionIndex, Value = incoming.Value, Timestamp = incoming.Timestamp }, cancellationToken: ct);
        await db.PublishAsync(changeChannel, changePayload);

        return true;
    }

    public async Task<Dictionary<ushort, ChannelValueSnapshot>> GetAllAsync(string carKey, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var hashKey = new RedisKey(string.Format(Consts.CAR_CHANNEL_STATE_KEY, carKey));
        var entries = await db.HashGetAllAsync(hashKey);

        var result = new Dictionary<ushort, ChannelValueSnapshot>(entries.Length);
        foreach (var entry in entries)
        {
            if (!ushort.TryParse(entry.Name.ToString(), out var index)) continue;
            var snapshot = MessagePackSerializer.Deserialize<ChannelValueSnapshot>((byte[])entry.Value!, cancellationToken: ct);
            result[index] = snapshot;
        }
        return result;
    }

    public async Task ClearAsync(string carKey, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var hashKey = new RedisKey(string.Format(Consts.CAR_CHANNEL_STATE_KEY, carKey));
        var deleted = await db.KeyDeleteAsync(hashKey);
        if (deleted)
            logger.LogInformation("Cleared channel state for car {CarKey}", carKey);
    }
}
