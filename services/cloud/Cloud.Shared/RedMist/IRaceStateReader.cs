using System.Text.Json;
using Cloud.Shared;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Cloud.Shared.RedMist;

/// <summary>
/// Reads the cached <see cref="RaceStateDto"/> for a team. Used by <c>WebHub</c> to seed
/// a fresh subscriber with the most recent state without waiting for the next pub/sub tick.
/// </summary>
public interface IRaceStateReader
{
    /// <summary>Returns the cached state, or <c>null</c> when no RedMist data is currently flowing.</summary>
    Task<RaceStateDto?> GetAsync(int teamId, CancellationToken ct);
}

public sealed class RaceStateReader(
    IConnectionMultiplexer redis,
    ILogger<RaceStateReader> logger) : IRaceStateReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<RaceStateDto?> GetAsync(int teamId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var raw = await db.StringGetAsync(string.Format(Consts.RACE_STATE_KEY, teamId));
        if (raw.IsNullOrEmpty) return null;

        try
        {
            return JsonSerializer.Deserialize<RaceStateDto>(raw.ToString(), JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Corrupt race-state JSON for team {TeamId}", teamId);
            return null;
        }
    }
}
