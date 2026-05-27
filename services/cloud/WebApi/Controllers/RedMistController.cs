using Cloud.Shared.Auth;
using Cloud.Shared.RedMist;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using RedMist.TimingCommon.Models;
using StackExchange.Redis;
using System.Text.Json;

namespace WebApi.Controllers;

/// <summary>
/// Read-only proxy for RedMist event metadata and the per-team connection-status read.
/// See ADR-0008. All endpoints require an authenticated race-management user;
/// team credentials never leave the server.
/// </summary>
[Route("v{version:apiVersion}/red-mist/[action]")]
[ApiVersion("1.0")]
[Produces("application/json")]
public sealed class RedMistController(
    ITeamRoleContext teamRoleContext,
    IRedMistRestClient redMist,
    IConnectionMultiplexer redis,
    HybridCache cache,
    TimeProvider time,
    ILogger<RedMistController> logger) : Controller
{
    private static readonly HybridCacheEntryOptions EventsCacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(60),
        LocalCacheExpiration = TimeSpan.FromSeconds(60),
    };

    /// <summary>
    /// Returns RedMist events from the team's perspective for the engineer's Race-pairing
    /// picker. Window is <c>userNow - 7 days</c> through RedMist's 30-day forward cap
    /// (ADR-0008). Cached for 60 seconds per team.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Event>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status424FailedDependency)]
    public async Task<ActionResult<IReadOnlyList<Event>>> LoadEventsLiveAndUpcomingAsync(int teamId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) return Forbid();

        var startDateUtc = time.GetUtcNow().UtcDateTime.AddDays(-7);
        var cacheKey = $"redmist:events-upcoming:{teamId}";

        try
        {
            return Ok(await cache.GetOrCreateAsync<IReadOnlyList<Event>>(
                cacheKey,
                async innerCt => await redMist.LoadEventsAsync(teamId, startDateUtc, innerCt),
                EventsCacheOptions,
                cancellationToken: ct));
        }
        catch (RedMistAuthException ex)
        {
            logger.LogWarning(ex, "RedMist events lookup auth-failed for team {TeamId}", teamId);
            return StatusCode(StatusCodes.Status424FailedDependency, new { error = "redmist-auth-failed", message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "RedMist events lookup transport failure for team {TeamId}", teamId);
            return StatusCode(StatusCodes.Status424FailedDependency, new { error = "redmist-unavailable", message = ex.Message });
        }
    }

    /// <summary>Returns a single RedMist event by id (with its sessions).</summary>
    [HttpGet]
    [ProducesResponseType<Event>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status424FailedDependency)]
    public async Task<ActionResult<Event>> LoadEventAsync(int teamId, int eventId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) return Forbid();

        try
        {
            var ev = await redMist.LoadEventAsync(teamId, eventId, ct);
            if (ev is null) return NotFound();
            return Ok(ev);
        }
        catch (RedMistAuthException ex)
        {
            logger.LogWarning(ex, "RedMist event lookup auth-failed for team {TeamId}, event {EventId}", teamId, eventId);
            return StatusCode(StatusCodes.Status424FailedDependency, new { error = "redmist-auth-failed", message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "RedMist event lookup transport failure for team {TeamId}, event {EventId}", teamId, eventId);
            return StatusCode(StatusCodes.Status424FailedDependency, new { error = "redmist-unavailable", message = ex.Message });
        }
    }

    /// <summary>
    /// Lists every RedMist organization for the Race-form picker. Returned sorted by name
    /// (case-insensitive) so the UI can render the dropdown without re-sorting. Cached for
    /// 60 seconds per team — orgs change rarely and the call is cheap.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrganizationSummary>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status424FailedDependency)]
    public async Task<ActionResult<IReadOnlyList<OrganizationSummary>>> LoadOrganizationsAsync(int teamId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) return Forbid();

        var cacheKey = $"redmist:organizations:{teamId}";

        try
        {
            return Ok(await cache.GetOrCreateAsync<IReadOnlyList<OrganizationSummary>>(
                cacheKey,
                async innerCt =>
                {
                    var orgs = await redMist.LoadOrganizationsAsync(teamId, innerCt);
                    return orgs
                        .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                },
                EventsCacheOptions,
                cancellationToken: ct));
        }
        catch (RedMistAuthException ex)
        {
            logger.LogWarning(ex, "RedMist organizations lookup auth-failed for team {TeamId}", teamId);
            return StatusCode(StatusCodes.Status424FailedDependency, new { error = "redmist-auth-failed", message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "RedMist organizations lookup transport failure for team {TeamId}", teamId);
            return StatusCode(StatusCodes.Status424FailedDependency, new { error = "redmist-unavailable", message = ex.Message });
        }
    }

    /// <summary>
    /// Returns the per-team RedMist connection status written by ChannelProcessor's
    /// <c>RedmistConsumer</c>. <c>null</c> when no status has ever been written for the team.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<RedMistConnectionStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RedMistConnectionStatusDto>> GetConnectionStatusAsync(int teamId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) return Forbid();

        var db = redis.GetDatabase();
        var json = await db.StringGetAsync(string.Format(RedMistConsts.STATUS_KEY, teamId));
        if (json.IsNullOrEmpty) return NoContent();

        try
        {
            var dto = JsonSerializer.Deserialize<RedMistConnectionStatusDto>(json.ToString());
            return dto is null ? NoContent() : Ok(dto);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Corrupt RedMist connection-status JSON for team {TeamId}", teamId);
            return NoContent();
        }
    }
}
