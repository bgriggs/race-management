using System.Collections.Concurrent;
using System.Text.Json;
using Channels;
using Cloud.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Cloud.Shared.Streaming;

public class CarChannelDefinitionResolver(
    IConnectionMultiplexer redis,
    IDbContextFactory<RaceManagementContext> dbFactory,
    ILogger<CarChannelDefinitionResolver> logger) : ICarChannelDefinitionResolver
{
    private readonly ConcurrentDictionary<Guid, Task<CachedMaps?>> _cache = new();

    public async Task<IReadOnlyDictionary<ushort, ChannelDefinition>?> GetSessionIndexMapAsync(string carKey, CancellationToken ct)
    {
        var cached = await ResolveAsync(carKey, ct);
        return cached?.SessionIndexMap;
    }

    public async Task<IReadOnlyDictionary<Guid, ushort>?> GetChannelIdMapAsync(string carKey, CancellationToken ct)
    {
        var cached = await ResolveAsync(carKey, ct);
        return cached?.ChannelIdMap;
    }

    private async Task<CachedMaps?> ResolveAsync(string carKey, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var activeConfigKey = string.Format(Consts.CAR_ACTIVE_CONFIG_KEY, carKey);
        var configIdRaw = await db.StringGetAsync(activeConfigKey);
        if (!configIdRaw.HasValue || !Guid.TryParse(configIdRaw.ToString(), out var configurationId))
        {
            logger.LogDebug("No active configuration found for car {CarKey}", carKey);
            return null;
        }

        // Cache key is the immutable configurationId — once a configuration is published
        // it never mutates in place; a new edit becomes a new row with a new Id. The car's
        // active-config pointer flips to the new Id on next SendChannelValuesAsync, which
        // naturally evicts the stale map by skipping the old cache entry.
        var cached = await _cache.GetOrAdd(configurationId, id => LoadAsync(id));
        if (cached is null)
        {
            _cache.TryRemove(configurationId, out _);
        }
        return cached;
    }

    private async Task<CachedMaps?> LoadAsync(Guid configurationId)
    {
        // Intentionally not threading the caller's CT here: the load is shared across all
        // concurrent callers via GetOrAdd, and one caller's cancellation must not cascade
        // to others awaiting the same task.
        await using var context = await dbFactory.CreateDbContextAsync();
        var row = await context.CarConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == configurationId);

        if (row is null)
        {
            logger.LogWarning("Active configuration {ConfigurationId} not found in database", configurationId);
            return null;
        }

        CarConfigSlice? slice;
        try
        {
            slice = JsonSerializer.Deserialize<CarConfigSlice>(row.ConfigurationJson);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize ConfigurationJson for configuration {ConfigurationId}", configurationId);
            return null;
        }

        var definitions = slice?.ChannelDefinitions ?? [];
        var sessionIndexMap = new Dictionary<ushort, ChannelDefinition>(definitions.Count);
        var channelIdMap = new Dictionary<Guid, ushort>(definitions.Count);
        for (var i = 0; i < definitions.Count; i++)
        {
            var def = definitions[i];
            var index = (ushort)i;
            sessionIndexMap[index] = def;
            // Last-write-wins on duplicate ChannelIds within a single car's configuration.
            // Duplicates shouldn't occur (the configuration editor enforces uniqueness),
            // but the dictionary indexer is defensive against it crashing the load.
            channelIdMap[def.Id] = index;
        }
        return new CachedMaps(sessionIndexMap, channelIdMap);
    }

    private sealed record CachedMaps(
        IReadOnlyDictionary<ushort, ChannelDefinition> SessionIndexMap,
        IReadOnlyDictionary<Guid, ushort> ChannelIdMap);

    // Local slice of CarConfiguration containing only the fields needed for routing.
    // Avoids taking a project reference on Common just for one DTO.
    private sealed class CarConfigSlice
    {
        public List<ChannelDefinition> ChannelDefinitions { get; set; } = [];
    }
}
