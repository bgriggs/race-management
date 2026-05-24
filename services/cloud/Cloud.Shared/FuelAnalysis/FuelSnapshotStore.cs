using MessagePack;
using StackExchange.Redis;

namespace Cloud.Shared.FuelAnalysis;

public sealed class FuelSnapshotStore(IConnectionMultiplexer redis) : IFuelSnapshotStore
{
    public async Task SetAsync(string carKey, FuelRangeSnapshot snapshot, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.FUEL_SNAPSHOT_KEY, carKey));
        var payload = MessagePackSerializer.Serialize(snapshot, cancellationToken: ct);
        await db.StringSetAsync(key, payload);
    }

    public async Task<FuelRangeSnapshot?> GetAsync(string carKey, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.FUEL_SNAPSHOT_KEY, carKey));
        var raw = await db.StringGetAsync(key);
        if (!raw.HasValue) return null;
        return MessagePackSerializer.Deserialize<FuelRangeSnapshot>((byte[])raw!, cancellationToken: ct);
    }

    public async Task ClearAsync(string carKey, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key = new RedisKey(string.Format(Consts.FUEL_SNAPSHOT_KEY, carKey));
        await db.KeyDeleteAsync(key);
    }
}
