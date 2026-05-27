namespace Cloud.Shared.RedMist;

/// <summary>
/// Acquires Keycloak Client Credentials access tokens for a team's RedMist credentials.
/// Implementations are expected to cache tokens until just before their expiry.
/// </summary>
public interface IRedMistTokenProvider
{
    /// <summary>
    /// Returns a valid access token for the team's configured RedMist credentials, or
    /// <c>null</c> when the team has no RedMist credentials configured — RedMist is opt-in,
    /// not a system requirement, so missing credentials are not an error. Reads
    /// <c>SiteSettings.RedMistClientId</c> / <c>RedMistClientSecret</c> from Postgres.
    /// Throws <see cref="RedMistAuthException"/> when credentials are present but Keycloak
    /// rejects them or returns a malformed response.
    /// </summary>
    Task<string?> GetAccessTokenAsync(int teamId, CancellationToken ct);

    /// <summary>
    /// Drops any cached token for the team — call after a 401 from RedMist to force a refresh.
    /// </summary>
    void InvalidateToken(int teamId);
}

/// <summary>
/// Raised when team RedMist credentials are present but Keycloak rejects them or returns
/// a malformed response. Missing credentials are reported as a <c>null</c> token from
/// <see cref="IRedMistTokenProvider.GetAccessTokenAsync"/>, not via this exception.
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
