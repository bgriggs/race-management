using System.Text.Json;
using Cloud.Shared;
using Cloud.Shared.Database;
using Common.FuelAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using StackExchange.Redis;

namespace ChannelProcessor.FuelAnalysis.Config;

public sealed class CarFuelConfigReader(
    IConnectionMultiplexer redis,
    HybridCache cache,
    IDbContextFactory<RaceManagementContext> dbFactory,
    ILogger<CarFuelConfigReader> logger) : ICarFuelConfigReader
{
    // Cache the parsed FuelConfig by configurationId (immutable) for longer than
    // RaceSessionGate's 5s gate, since configurations rarely change during a race.
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(5),
    };

    public async Task<CarFuelConfig?> GetAsync(string carKey, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var raw = await db.StringGetAsync(string.Format(Consts.CAR_ACTIVE_CONFIG_KEY, carKey));
        if (!raw.HasValue || !Guid.TryParse(raw.ToString(), out var configurationId)) return null;

        var result = await cache.GetOrCreateAsync(
            $"fuel:carconfig:{configurationId}",
            (id: configurationId, factory: dbFactory, log: logger),
            static async (state, innerCt) =>
            {
                await using var db = await state.factory.CreateDbContextAsync(innerCt);
                var row = await db.CarConfigurations
                    .AsNoTracking()
                    .Where(c => c.Id == state.id)
                    .Select(c => c.ConfigurationJson)
                    .FirstOrDefaultAsync(innerCt);
                if (row is null) return null;
                try
                {
                    var slice = JsonSerializer.Deserialize<FuelConfigSlice>(row);
                    return slice?.FuelConfig;
                }
                catch (JsonException ex)
                {
                    state.log.LogError(ex, "Failed to parse FuelConfig from CarConfiguration {ConfigurationId}", state.id);
                    return null;
                }
            },
            CacheOptions,
            cancellationToken: ct);

        return result;
    }

    private sealed class FuelConfigSlice
    {
        public CarFuelConfig? FuelConfig { get; set; }
    }
}
