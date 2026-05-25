using Channels;
using Cloud.Shared;
using Cloud.Shared.Streaming;
using Cloud.Shared.Telemetry;
using MessagePack;
using StackExchange.Redis;

namespace ChannelProcessor.Alarms.Channel;

/// <summary>
/// Car-scoped <see cref="IChannelRepository"/> used during a single alarm evaluation
/// pass. Reads dispatch by <see cref="ChannelDefinition.Scope"/>: <c>PerCar</c> reads
/// the car's Redis hash via SessionIndex; <c>PerTeam</c> reads the team's hash by
/// stable ChannelId Guid. Writes (used when an alarm has an <c>AlarmStatusChannelId</c>)
/// publish to the telemetry stream via the matching publisher, with the channel's
/// configured Distribution governing whether the value reaches the car.
/// </summary>
public sealed class CarScopedChannelRepository(
    int teamId,
    string carNumber,
    string carKey,
    IReadOnlyDictionary<Guid, ChannelDefinition> definitionsById,
    IReadOnlyDictionary<Guid, ushort> sessionIndexByChannelId,
    IConnectionMultiplexer redis,
    ICarChannelPublisher carPublisher,
    ITeamChannelPublisher teamPublisher,
    TimeProvider timeProvider,
    ILogger logger,
    CancellationToken cancellationToken) : IChannelRepository
{
    public async Task<ChannelValue> GetChannelValueAsync(Guid channelId)
    {
        if (!definitionsById.TryGetValue(channelId, out var def))
            return new ChannelValue();

        var db = redis.GetDatabase();

        if (def.Scope == ChannelScope.PerTeam)
        {
            var hashKey = new RedisKey(string.Format(Consts.TEAM_CHANNEL_STATE_KEY, teamId));
            var raw = await db.HashGetAsync(hashKey, new RedisValue(channelId.ToString()));
            return DeserializeOrEmpty(raw);
        }

        if (!sessionIndexByChannelId.TryGetValue(channelId, out var sessionIndex))
            return new ChannelValue();

        var carHashKey = new RedisKey(string.Format(Consts.CAR_CHANNEL_STATE_KEY, carKey));
        var carRaw = await db.HashGetAsync(carHashKey, new RedisValue(sessionIndex.ToString()));
        return DeserializeOrEmpty(carRaw);
    }

    public async Task SetChannelValueAsync(Guid channelId, ChannelValue ch)
    {
        if (!definitionsById.TryGetValue(channelId, out var def))
        {
            logger.LogWarning("Cannot publish alarm status: channel {ChannelId} not in active configuration for car {CarKey}", channelId, carKey);
            return;
        }

        // AlarmEvaluation builds a ChannelValue with only Value set; stamp a timestamp
        // so downstream consumers (logging, change-detection) have a usable one.
        var timestamp = ch.Timestamp == default ? timeProvider.GetUtcNow().UtcDateTime : ch.Timestamp;

        if (def.Scope == ChannelScope.PerTeam)
        {
            await teamPublisher.PublishAsync(teamId, new[]
            {
                new TeamChannelValue { ChannelId = channelId, Value = ch.Value, Timestamp = timestamp },
            }, cancellationToken);
        }
        else
        {
            await carPublisher.PublishAsync(teamId, carNumber, new[]
            {
                new PublishedChannelValue(channelId, ch.Value, timestamp),
            }, cancellationToken);
        }
    }

    private static ChannelValue DeserializeOrEmpty(RedisValue raw)
    {
        if (!raw.HasValue) return new ChannelValue();
        var snapshot = MessagePackSerializer.Deserialize<ChannelValueSnapshot>((byte[])raw!);
        return new ChannelValue { Value = snapshot.Value, Timestamp = snapshot.Timestamp };
    }
}
