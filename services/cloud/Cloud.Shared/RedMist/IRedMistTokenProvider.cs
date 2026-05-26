namespace Cloud.Shared.RedMist;

/// <summary>
/// Acquires Keycloak Client Credentials access tokens for a team's RedMist credentials.
/// Implementations are expected to cache tokens until just before their expiry.
/// </summary>
public interface IRedMistTokenProvider
{
    /// <summary>
    /// Returns a valid access token for the team's configured RedMist credentials. Reads
    /// <c>SiteSettings.RedMistClientId</c> / <c>RedMistClientSecret</c> from Postgres.
    /// Throws <see cref="RedMistAuthException"/> on missing credentials or Keycloak 401/403.
    /// </summary>
    Task<string> GetAccessTokenAsync(int teamId, CancellationToken ct);

    /// <summary>
    /// Drops any cached token for the team — call after a 401 from RedMist to force a refresh.
    /// </summary>
    void InvalidateToken(int teamId);
}

/// <summary>
/// Raised when team RedMist credentials are missing or RedMist Keycloak rejects them.
/// Callers surface this through <see cref="RedMistConnectionState.AuthFailed"/>.
/// </summary>
public sealed class RedMistAuthException : Exception
{
    public int TeamId { get; }
    public RedMistAuthException(int teamId, string message, Exception? inner = null)
        : base(message, inner)
    {
        TeamId = teamId;
    }
}
