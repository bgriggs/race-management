using System.Globalization;
using Channels.Logic;
using Cloud.Shared;
using StackExchange.Redis;

namespace ChannelProcessor.Alarms.State;

public sealed class RedisComparisonDurationRepository(IConnectionMultiplexer redis, string carKey) : IComparisonDurationRepository
{
    public async Task<DateTimeOffset?> GetStartTimeAsync(Guid comparisonId)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.ALARM_COMPARISON_DURATION_KEY, carKey, comparisonId));
        var raw = await db.StringGetAsync(key);
        if (!raw.HasValue) return null;
        if (!long.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)) return null;
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    public async Task SetStartTimeAsync(Guid comparisonId, DateTimeOffset startTime)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.ALARM_COMPARISON_DURATION_KEY, carKey, comparisonId));
        await db.StringSetAsync(key, startTime.UtcTicks.ToString(CultureInfo.InvariantCulture), null, When.Always, CommandFlags.None);
    }

    public async Task RemoveStartTimeAsync(Guid comparisonId)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.ALARM_COMPARISON_DURATION_KEY, carKey, comparisonId));
        await db.KeyDeleteAsync(key);
    }
}
