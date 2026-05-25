using System.Text.Json;
using Channels.Alarms;
using StackExchange.Redis;

namespace Cloud.Shared.Alarms;

public sealed class RedisAlarmStateGateway(IConnectionMultiplexer redis) : IRedisAlarmStateGateway
{
    public async Task<AlarmState> GetAsync(string carKey, Guid alarmId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.ALARM_STATE_KEY, carKey, alarmId));
        var raw = await db.StringGetAsync(key);
        if (!raw.HasValue) return new AlarmState { Id = alarmId };

        var state = JsonSerializer.Deserialize<AlarmState>(raw.ToString()) ?? new AlarmState { Id = alarmId };
        if (state.Id == Guid.Empty) state.Id = alarmId;
        return state;
    }

    public async Task SetAsync(string carKey, AlarmState state, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.ALARM_STATE_KEY, carKey, state.Id));
        // Pin the legacy 5-arg overload so the SUT and tests see the same signature.
        await db.StringSetAsync(key, JsonSerializer.Serialize(state), null, When.Always, CommandFlags.None);
    }

    public async Task<AlarmState> AcknowledgeAsync(string carKey, Guid alarmId, DateTime utcNow, CancellationToken ct = default)
    {
        var state = await GetAsync(carKey, alarmId, ct);
        if (state.IsAcknowledged) return state;

        state.IsAcknowledged = true;
        state.LastAcknowledgedTimestamp = utcNow;
        await SetAsync(carKey, state, ct);
        return state;
    }
}
