namespace Cloud.Shared.RedMist;

/// <summary>
/// Configuration for the upstream RedMist endpoints. Bound from the <c>RedMist</c>
/// configuration section. All fields fall back to the defaults in <see cref="RedMistConsts"/>
/// so production deployments do not need to set anything explicitly.
/// </summary>
public sealed class RedMistOptions
{
    public const string SectionName = "RedMist";

    /// <summary>Keycloak base URL hosting the RedMist realm (e.g., <c>https://auth.redmist.racing</c>).</summary>
    public string AuthServerUrl { get; set; } = RedMistConsts.DEFAULT_AUTH_SERVER_URL;

    /// <summary>Keycloak realm (default: <c>redmist</c>).</summary>
    public string Realm { get; set; } = RedMistConsts.DEFAULT_REALM;

    /// <summary>Base URL for the Status REST API (e.g., <c>https://api.redmist.racing/status</c>).</summary>
    public string StatusApiUrl { get; set; } = RedMistConsts.DEFAULT_STATUS_API_URL;

    /// <summary>Full URL for the <c>StatusHub</c> SignalR endpoint.</summary>
    public string HubUrl { get; set; } = RedMistConsts.DEFAULT_HUB_URL;
}
