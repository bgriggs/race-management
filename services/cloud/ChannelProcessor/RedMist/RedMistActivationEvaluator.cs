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
    /// Returns the set of teams that have at least one RedMist-paired race row OR an
    /// explicit <see cref="Team.SelectedRaceId"/>, so the consumer worker only iterates
    /// teams that could possibly need a subscription. "Paired" means either an explicit
    /// <c>RedMistEventId</c> OR a <c>RedMistOrganizationId</c> (the latter resolves to
    /// the org's currently-live event at attach time).
    /// </summary>
    public async Task<IReadOnlyList<int>> ListCandidateTeamsAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var fromRaces = await db.Races
            .AsNoTracking()
            .Where(r => r.RedMistEventId != null || r.RedMistOrganizationId != null)
            .Select(r => r.TeamId)
            .Distinct()
            .ToListAsync(ct);
        var fromSelection = await db.Teams
            .AsNoTracking()
            .Where(t => t.SelectedRaceId != null)
            .Select(t => t.Id)
            .ToListAsync(ct);
        return fromRaces.Union(fromSelection).ToList();
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
    /// Picks the Race for <paramref name="teamId"/> whose window currently covers <paramref name="nowUtc"/>.
    /// If <see cref="Team.SelectedRaceId"/> is set, that Race is the *only* candidate
    /// considered — picking an unpaired race or a future/stale race terminates any
    /// existing subscription rather than silently falling back to another race. The user's
    /// explicit choice always wins; to get the time-window auto-pick back, the selection
    /// must be cleared. When the selection is null, falls back to the auto-pick: smallest
    /// |Start - now| over rows with either an explicit event id or an org id, applying
    /// the pre/post pad. The worker resolves org-only candidates to a live event id via
    /// RedMist before attach.
    /// </summary>
    public async Task<ActivationCandidate?> SelectCandidateAsync(int teamId, DateTime nowUtc, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Explicit selection short-circuits the auto-pick. The user said "watch this race";
        // honor it even when the picked race is unpaired (detach) or out of window (no attach).
        var selectedRaceId = await db.Teams
            .AsNoTracking()
            .Where(t => t.Id == teamId)
            .Select(t => t.SelectedRaceId)
            .FirstOrDefaultAsync(ct);

        if (selectedRaceId is int sid)
        {
            var selected = await db.Races
                .AsNoTracking()
                .Where(r => r.Id == sid && r.TeamId == teamId)
                .Select(r => new { r.Id, r.Name, r.Start, r.Duration, r.TimeZone, r.RedMistEventId, r.RedMistOrganizationId, r.RedMistAccessCode })
                .FirstOrDefaultAsync(ct);

            // Selected race vanished (DeleteRace should have cleared the selection but be
            // defensive against drift) — treat as no candidate. The user gets a blank header
            // rather than a surprise subscription to a different race.
            if (selected is null)
            {
                logger.LogDebug("Team {TeamId} SelectedRaceId={RaceId} no longer exists; no candidate this tick", teamId, sid);
                return null;
            }

            // User picked an unpaired race — explicit "stop monitoring RedMist" signal.
            // Return null so the worker detaches; do NOT fall back to auto-pick.
            if (selected.RedMistEventId is null && selected.RedMistOrganizationId is null)
            {
                logger.LogDebug("Team {TeamId} SelectedRaceId={RaceId} has no RedMist pairing; detaching", teamId, sid);
                return null;
            }

            if (!TryGetZone(selected.TimeZone, out var stz))
            {
                logger.LogWarning("Race {RaceId} (team {TeamId}) selected but has unknown TimeZone '{TimeZone}'; no candidate", selected.Id, teamId, selected.TimeZone);
                return null;
            }

            var startUtc = TimeZoneInfo.ConvertTimeToUtc(selected.Start, stz);
            var endUtc = startUtc.AddHours(selected.Duration);
            return new ActivationCandidate(
                RaceId: selected.Id,
                RaceName: selected.Name,
                RedMistEventId: selected.RedMistEventId,
                RedMistOrganizationId: selected.RedMistOrganizationId,
                RedMistAccessCode: selected.RedMistAccessCode,
                StartUtc: startUtc,
                EndUtc: endUtc,
                InWindow: nowUtc >= startUtc - PrePad && nowUtc <= endUtc + PostPad);
        }

        var rows = await db.Races
            .AsNoTracking()
            .Where(r => r.TeamId == teamId && (r.RedMistEventId != null || r.RedMistOrganizationId != null))
            .Select(r => new { r.Id, r.Name, r.Start, r.Duration, r.TimeZone, r.RedMistEventId, r.RedMistOrganizationId, r.RedMistAccessCode })
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
                    RedMistEventId: r.RedMistEventId,
                    RedMistOrganizationId: r.RedMistOrganizationId,
                    RedMistAccessCode: r.RedMistAccessCode,
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
/// The currently-best candidate race row for a team. Exactly one of <see cref="RedMistEventId"/>
/// or <see cref="RedMistOrganizationId"/> is set when the Race was configured for that mode;
/// both may be set when the user supplied both (event id wins). <see cref="InWindow"/> is the
/// time-only predicate result — when <c>false</c>, the caller may still subscribe if RedMist
/// reports the event live (ADR-0008 "IsLive extension"). <see cref="RedMistAccessCode"/> is
/// the optional private-event code passed to <c>SubscribeToEventV2WithCode</c>; <c>null</c>
/// for public events. The caller is expected to consult <c>LoadLiveEvents</c> to decide and
/// to resolve org-only candidates to a concrete event id.
/// </summary>
public sealed record ActivationCandidate(
    int RaceId,
    string RaceName,
    int? RedMistEventId,
    int? RedMistOrganizationId,
    string? RedMistAccessCode,
    DateTime StartUtc,
    DateTime EndUtc,
    bool InWindow);
