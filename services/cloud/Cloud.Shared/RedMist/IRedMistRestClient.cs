using RedMist.TimingCommon.Models;

namespace Cloud.Shared.RedMist;

/// <summary>
/// Thin wrapper over RedMist's Status REST API. All calls authenticate using the team's
/// configured RedMist credentials via <see cref="IRedMistTokenProvider"/>.
/// </summary>
public interface IRedMistRestClient
{
    /// <summary>
    /// Returns events whose <c>StartDate &gt;= startDateUtc</c>. RedMist caps the range at
    /// 30 days; the engineer-facing event picker uses <c>userNow - 7 days</c> (see ADR-0008).
    /// </summary>
    Task<IReadOnlyList<Event>> LoadEventsAsync(int teamId, DateTime startDateUtc, CancellationToken ct);

    /// <summary>Loads one event with its sessions. RedMist endpoint is anonymous; token still sent if present.</summary>
    Task<Event?> LoadEventAsync(int teamId, int eventId, CancellationToken ct);

    /// <summary>Loads currently-live events (anonymous; team credentials optional).</summary>
    Task<IReadOnlyList<EventListSummary>> LoadLiveEventsAsync(int teamId, CancellationToken ct);

    /// <summary>
    /// Fetches the full current <see cref="SessionState"/> snapshot for the given event.
    /// Used by RedmistConsumer on every connect/reconnect to re-emit current channel values.
    /// </summary>
    Task<SessionState?> GetCurrentSessionStateAsync(int teamId, int eventId, CancellationToken ct);

    /// <summary>Loads completed laps for one car (used by competitor-analysis proxy and gap recovery).</summary>
    Task<IReadOnlyList<CarPosition>> LoadCarLapsAsync(int teamId, int eventId, int sessionId, string carNumber, CancellationToken ct);

    /// <summary>Loads completed laps for every car in the session (used by competitor-analysis proxy).</summary>
    Task<IReadOnlyList<CarPosition>> LoadSessionLapsAsync(int teamId, int eventId, int sessionId, CancellationToken ct);
}
