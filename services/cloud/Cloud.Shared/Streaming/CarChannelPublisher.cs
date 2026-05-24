using Channels;
using MessagePack;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Cloud.Shared.Streaming;

public class CarChannelPublisher(
    IConnectionMultiplexer redis,
    ICarChannelDefinitionResolver resolver,
    ILogger<CarChannelPublisher> logger) : ICarChannelPublisher
{
    public async Task<int> PublishAsync(int teamId, string car, IReadOnlyList<PublishedChannelValue> values, CancellationToken ct)
    {
        if (values.Count == 0) return 0;

        var carKey = string.Format(Consts.CAR_STREAM_FIELD, teamId, car);
        var channelIdMap = await resolver.GetChannelIdMapAsync(carKey, ct);
        if (channelIdMap is null)
        {
            logger.LogWarning("Car {CarKey} has no active configuration; dropping {Count} channel values", carKey, values.Count);
            return 0;
        }

        var resolved = new List<ChannelValue>(values.Count);
        foreach (var v in values)
        {
            if (channelIdMap.TryGetValue(v.ChannelId, out var sessionIndex))
            {
                resolved.Add(new ChannelValue { SessionIndex = sessionIndex, Value = v.Value, Timestamp = v.Timestamp });
            }
            else
            {
                logger.LogWarning("Channel {ChannelId} not in active configuration for car {CarKey}; value dropped", v.ChannelId, carKey);
            }
        }

        if (resolved.Count == 0) return 0;

        var db = redis.GetDatabase();
        var payload = MessagePackSerializer.Serialize(resolved.ToArray(), cancellationToken: ct);
        await db.StreamAddAsync(Consts.CAR_CHANNEL_VALUES_STREAM_KEY, carKey, payload);

        logger.LogDebug("Published {Count} channel values to car {CarKey}", resolved.Count, carKey);
        return resolved.Count;
    }
}
