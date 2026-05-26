namespace Cloud.Shared.RedMist;

/// <summary>
/// Connection state for the per-team RedMist hub subscription. Written by RedmistConsumer
/// to <see cref="RedMistConsts.STATUS_KEY"/>; read on demand by WebApi for the UI status pill.
/// </summary>
public enum RedMistConnectionState
{
    /// <summary>No paired Race row is currently in the activation window; consumer is idle for this team.</summary>
    NoEventPaired = 0,
    /// <summary>Acquiring Keycloak token and opening the hub.</summary>
    Connecting = 1,
    /// <summary>Hub subscribed and receiving updates.</summary>
    Connected = 2,
    /// <summary>Hub disconnected; the SignalR client is retrying.</summary>
    Reconnecting = 3,
    /// <summary>Keycloak rejected the team's credentials. Will not retry until the engineer updates credentials.</summary>
    AuthFailed = 4,
    /// <summary>Subscription torn down (lease lost, shutdown, or Race left the activation window).</summary>
    Disconnected = 5,
}

/// <summary>
/// Wire format for the per-team RedMist connection status. Stored in Redis as JSON.
/// </summary>
public sealed class RedMistConnectionStatusDto
{
    public required RedMistConnectionState State { get; set; }
    public required DateTime LastChangeAtUtc { get; set; }
    /// <summary>RedMist event id currently subscribed, when <see cref="State"/> is <c>Connected</c> or <c>Reconnecting</c>.</summary>
    public int? EventId { get; set; }
    /// <summary>Optional detail line for the UI (e.g., reason for AuthFailed).</summary>
    public string? Detail { get; set; }
}
