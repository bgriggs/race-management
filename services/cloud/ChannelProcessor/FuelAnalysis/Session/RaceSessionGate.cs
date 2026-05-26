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
                var nowUtc = state.time.GetUtcNow().UtcDateTime;
                await using var db = await state.factory.CreateDbContextAsync(innerCt);
                // Race.Start is naive wall-clock in Race.TimeZone (see races.ts comment
                // about preserving the typed string verbatim through the round-trip). We
                // can't filter "active right now" in SQL because each row may resolve to
                // a different UTC offset, so pull the team's rows and check in memory.
                // Race counts per team are small (≲ tens), and the surrounding 5s cache
                // amortizes this away from the per-message hot path.
                var rows = await db.Races
                    .AsNoTracking()
                    .Where(r => r.TeamId == state.teamId)
                    .Select(r => new { r.Id, r.Start, r.Duration, r.TimeZone })
                    .ToListAsync(innerCt);

                int? activeId = null;
                DateTime mostRecentStartUtc = DateTime.MinValue;
                foreach (var r in rows)
                {
                    if (!TryGetZone(r.TimeZone, out var tz)) continue;
                    var nowInZone = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
                    if (r.Start > nowInZone) continue;
                    if (nowInZone >= r.Start.AddHours(r.Duration)) continue;
                    // Tie-break by UTC start so overlapping races in different zones
                    // resolve deterministically to the most-recently-started one,
                    // mirroring the prior OrderByDescending(r => r.Start) semantic.
                    var startUtc = TimeZoneInfo.ConvertTimeToUtc(r.Start, tz);
                    if (activeId is null || startUtc > mostRecentStartUtc)
                    {
                        activeId = r.Id;
                        mostRecentStartUtc = startUtc;
                    }
                }
                return activeId;
            },
            CacheOptions,
            cancellationToken: ct);
    }

    private static bool TryGetZone(string ianaId, out TimeZoneInfo tz)
    {
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(ianaId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.Utc;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            tz = TimeZoneInfo.Utc;
            return false;
        }
    }

    private static string CacheKey(int teamId) => $"fuel:active-race:{teamId}";
}
