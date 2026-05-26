using Cloud.Shared.Database;
using Cloud.Shared.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChannelProcessor.RedMist;

/// <summary>
/// Selects which paired <see cref="Race"/> a team's RedMist subscription should target, per
/// the activation rule in ADR-0008. Pure data class — no Redis, no HTTP.
/// </summary>
public sealed class RedMistActivationEvaluator
{
    private static readonly TimeSpan PrePad = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan PostPad = TimeSpan.FromMinutes(30);

    private readonly IDbContextFactory<RaceManagementContext> dbFactory;
    private readonly ILogger<RedMistActivationEvaluator> logger;

    public RedMistActivationEvaluator(
        IDbContextFactory<RaceManagementContext> dbFactory,
        ILogger<RedMistActivationEvaluator> logger)
    {
        this.dbFactory = dbFactory;
        this.logger = logger;
    }

    /// <summary>
    /// Returns the set of teams that have at least one RedMist-paired race row, so the
    /// consumer worker only iterates teams that could possibly need a subscription.
    /// </summary>
    public async Task<IReadOnlyList<int>> ListCandidateTeamsAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Races
            .AsNoTracking()
            .Where(r => r.RedMistEventId != null)
            .Select(r => r.TeamId)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Loads the team's set of <c>CarConfiguration.Car</c> numbers — the cars whose
    /// <c>CarPositionPatch</c> entries the consumer should forward to the channel pipeline
    /// (ADR-0008 car-mapping rule). Non-team cars are dropped silently.
    /// </summary>
    public async Task<IReadOnlySet<string>> LoadTeamCarNumbersAsync(int teamId, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.CarConfigurations
            .AsNoTracking()
            .Where(c => c.TeamId == teamId)
            .Select(c => c.Car)
            .ToListAsync(ct);
        return rows.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Picks the Race for <paramref name="teamId"/> whose window currently covers <paramref name="nowUtc"/>,
    /// applying the pre/post pad. Live-event extension (the OR-clause in ADR-0008) is layered
    /// on by the caller using the live-event list from RedMist; this method evaluates only the
    /// time-window predicate.
    /// </summary>
    public async Task<ActivationCandidate?> SelectCandidateAsync(int teamId, DateTime nowUtc, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Races
            .AsNoTracking()
            .Where(r => r.TeamId == teamId && r.RedMistEventId != null)
            .Select(r => new { r.Id, r.Name, r.Start, r.Duration, r.TimeZone, r.RedMistEventId, r.RedMistOrganizationId })
            .ToListAsync(ct);

        ActivationCandidate? best = null;
        var bestDelta = TimeSpan.MaxValue;

        foreach (var r in rows)
        {
            if (!TryGetZone(r.TimeZone, out var tz))
            {
                logger.LogWarning("Race {RaceId} (team {TeamId}) has unknown TimeZone '{TimeZone}'; skipping.", r.Id, teamId, r.TimeZone);
                continue;
            }

            var startUtc = TimeZoneInfo.ConvertTimeToUtc(r.Start, tz);
            var endUtc = startUtc.AddHours(r.Duration);

            if (nowUtc < startUtc - PrePad) continue;
            // Late-window cut-off is layered on by the caller (IsLive can extend the window).
            // Here we only screen out races that ended long ago.
            if (nowUtc > endUtc + PostPad + TimeSpan.FromHours(12)) continue;

            // Smallest |Start - now|, then smallest Id.
            var delta = (nowUtc - startUtc).Duration();
            if (best is null || delta < bestDelta || (delta == bestDelta && r.Id < best.RaceId))
            {
                best = new ActivationCandidate(
                    RaceId: r.Id,
                    RaceName: r.Name,
                    RedMistEventId: r.RedMistEventId!.Value,
                    StartUtc: startUtc,
                    EndUtc: endUtc,
                    InWindow: nowUtc <= endUtc + PostPad);
                bestDelta = delta;
            }
        }

        return best;
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
}

/// <summary>
/// The currently-best candidate race row for a team. <see cref="InWindow"/> is the
/// time-only predicate result — when <c>false</c>, the caller may still subscribe if RedMist
/// reports the event live (ADR-0008 "IsLive extension"). The caller is expected to consult
/// <c>LoadLiveEvents</c> to decide.
/// </summary>
public sealed record ActivationCandidate(
    int RaceId,
    string RaceName,
    int RedMistEventId,
    DateTime StartUtc,
    DateTime EndUtc,
    bool InWindow);
