namespace Cloud.Shared.RedMist;

/// <summary>
/// Constants for the RedMist.racing integration. See ADR-0008.
/// </summary>
public static class RedMistConsts
{
    /// <summary>
    /// Per-team subscription lease held by the ChannelProcessor replica that owns the team's
    /// active RedMist hub connection. <c>SET ... NX EX 60</c>; renewed every 30 s by the
    /// holder. Key is keyed by teamId only — the team is assumed to be in at most one event
    /// at a time (ADR-0008).
    /// </summary>
    public const string LEASE_KEY = "redmist:lease:{0}";

    /// <summary>
    /// Per-team RedMist connection state, written by RedmistConsumer and read on demand by
    /// WebApi's <c>GET /v1/redmist/connection-status</c> endpoint. JSON-serialized
    /// <see cref="RedMistConnectionStatusDto"/>.
    /// </summary>
    public const string STATUS_KEY = "redmist:status:{0}";

    /// <summary>
    /// Defaults for the upstream RedMist endpoints. Override via the <c>RedMist:*</c>
    /// configuration section (see <see cref="RedMistOptions"/>).
    /// </summary>
    public const string DEFAULT_AUTH_SERVER_URL = "https://auth.redmist.racing";
    public const string DEFAULT_REALM = "redmist";
    public const string DEFAULT_STATUS_API_URL = "https://api.redmist.racing/status";
    public const string DEFAULT_HUB_URL = "https://api.redmist.racing/status/event-status";
}
