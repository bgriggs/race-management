using Cloud.Shared;
using MessagePack;
using StackExchange.Redis;

namespace ChannelProcessor.FuelAnalysis.State;

public sealed class CarFuelStateRepository(IConnectionMultiplexer redis) : ICarFuelStateRepository
{
    public async Task<CarFuelState?> GetAsync(string carKey, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.FUEL_STATE_KEY, carKey));
        var raw = await db.StringGetAsync(key);
        if (!raw.HasValue) return null;
        return MessagePackSerializer.Deserialize<CarFuelState>((byte[])raw!, cancellationToken: ct);
    }

    public async Task SetAsync(string carKey, CarFuelState state, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.FUEL_STATE_KEY, carKey));
        var payload = MessagePackSerializer.Serialize(state, cancellationToken: ct);
        await db.StringSetAsync(key, payload);
    }

    public async Task ClearAsync(string carKey, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.FUEL_STATE_KEY, carKey));
        await db.KeyDeleteAsync(key);
    }
}
