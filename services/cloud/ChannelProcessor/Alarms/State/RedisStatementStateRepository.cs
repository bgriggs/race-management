using Channels.Logic;
using Cloud.Shared;
using StackExchange.Redis;

namespace ChannelProcessor.Alarms.State;

public sealed class RedisStatementStateRepository(IConnectionMultiplexer redis, string carKey) : IStatementStateRepository
{
    public async Task<bool?> GetStateAsync(Guid statementId)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.ALARM_STATEMENT_STATE_KEY, carKey, statementId));
        var raw = await db.StringGetAsync(key);
        if (!raw.HasValue) return null;
        return raw == "1";
    }

    public async Task SetStateAsync(Guid statementId, bool state)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.ALARM_STATEMENT_STATE_KEY, carKey, statementId));
        await db.StringSetAsync(key, state ? "1" : "0", null, When.Always, CommandFlags.None);
    }
}
