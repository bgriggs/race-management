using Cloud.Shared.Database;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace ChannelProcessor.FuelAnalysis.Calibration;

public sealed class CalibrationFactorReader(
    HybridCache cache,
    IDbContextFactory<RaceManagementContext> dbFactory) : ICalibrationFactorReader
{
    // Short TTL — WebApi-driven overrides land in another process so cache invalidation
    // only works within ChannelProcessor's own pod; the TTL bounds the cross-pod lag.
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(5),
        LocalCacheExpiration = TimeSpan.FromSeconds(5),
    };

    public async Task<CalibrationFactor?> GetLatestAsync(int teamId, string carNumber, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync(
            Key(teamId, carNumber),
            (teamId, carNumber, factory: dbFactory),
            static async (state, innerCt) =>
            {
                await using var db = await state.factory.CreateDbContextAsync(innerCt);
                return await db.CalibrationFactors
                    .AsNoTracking()
                    .Where(c => c.TeamId == state.teamId && c.CarNumber == state.carNumber)
                    .OrderByDescending(c => c.EffectiveAt)
                    .ThenByDescending(c => c.Id)
                    .FirstOrDefaultAsync(innerCt);
            },
            CacheOptions,
            cancellationToken: ct);
    }

    public async Task InvalidateAsync(int teamId, string carNumber, CancellationToken ct = default) =>
        await cache.RemoveAsync(Key(teamId, carNumber), ct);

    private static string Key(int teamId, string carNumber) => $"fuel:calibration:{teamId}:{carNumber}";
}
