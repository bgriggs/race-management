using Cloud.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace ChannelProcessor.FuelAnalysis.Session;

public sealed class RaceSessionGate(
    HybridCache cache,
    IDbContextFactory<RaceManagementContext> dbFactory,
    TimeProvider timeProvider) : IRaceSessionGate
{
    // Race.Duration is in hours (see ui/race-management-cloud/src/app/settings/races).
    private const double HoursToTicks = TimeSpan.TicksPerHour;

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
                // Single round-trip; we expect 0 or 1 active race per team but tolerate >1
                // by taking the most recently started.
                var active = await db.Races
                    .AsNoTracking()
                    .Where(r => r.TeamId == state.teamId
                                && r.Start <= now
                                && now < r.Start.AddTicks((long)(r.Duration * HoursToTicks)))
                    .OrderByDescending(r => r.Start)
                    .Select(r => (int?)r.Id)
                    .FirstOrDefaultAsync(innerCt);
                return active;
            },
            CacheOptions,
            cancellationToken: ct);
    }

    private static string CacheKey(int teamId) => $"fuel:active-race:{teamId}";
}
