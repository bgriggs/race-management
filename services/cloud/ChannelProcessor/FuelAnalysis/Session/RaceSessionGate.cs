using Cloud.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace ChannelProcessor.FuelAnalysis.Session;

public sealed class RaceSessionGate(
    HybridCache cache,
    IDbContextFactory<RaceManagementContext> dbFactory,
    TimeProvider timeProvider) : IRaceSessionGate
{
    // Short TTL: races change start/end on human time-scales but the active-race set
    // can flip exactly at the second boundary, so 5 s keeps the lookup off the hot path
    // without lagging real transitions noticeably.
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(5),
        LocalCacheExpiration = TimeSpan.FromSeconds(5),
    };

    public async Task<int?> GetActiveRaceIdAsync(int teamId, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            CacheKey(teamId),
            (teamId, factory: dbFactory, time: timeProvider),
            static async (state, innerCt) =>
            {
                var now = state.time.GetUtcNow().UtcDateTime;
                await using var db = await state.factory.CreateDbContextAsync(innerCt);
                // EF Core can't translate DateTime arithmetic (AddTicks/AddHours) to SQL,
                // so fetch the most-recently-started race that has begun and check the end
                // boundary in memory. At most one row crosses the wire.
                var candidate = await db.Races
                    .AsNoTracking()
                    .Where(r => r.TeamId == state.teamId && r.Start <= now)
                    .OrderByDescending(r => r.Start)
                    .Select(r => new { r.Id, r.Start, r.Duration })
                    .FirstOrDefaultAsync(innerCt);

                if (candidate is null) return (int?)null;
                var end = candidate.Start.AddHours(candidate.Duration);
                return now < end ? candidate.Id : null;
            },
            CacheOptions,
            cancellationToken: ct);
    }

    private static string CacheKey(int teamId) => $"fuel:active-race:{teamId}";
}
