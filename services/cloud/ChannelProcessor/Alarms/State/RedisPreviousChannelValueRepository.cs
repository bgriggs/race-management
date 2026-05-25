using Channels.Logic;
using Cloud.Shared;
using StackExchange.Redis;

namespace ChannelProcessor.Alarms.State;

public sealed class RedisPreviousChannelValueRepository(IConnectionMultiplexer redis, string carKey) : IPreviousChannelValueRepository
{
    public async Task<string?> GetPreviousValueAsync(Guid channelId)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.ALARM_PREVIOUS_VALUE_KEY, carKey, channelId));
        var raw = await db.StringGetAsync(key);
        return raw.HasValue ? raw.ToString() : null;
    }

    public async Task SetPreviousValueAsync(Guid channelId, string value)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.ALARM_PREVIOUS_VALUE_KEY, carKey, channelId));
        await db.StringSetAsync(key, value, null, When.Always, CommandFlags.None);
    }
}
