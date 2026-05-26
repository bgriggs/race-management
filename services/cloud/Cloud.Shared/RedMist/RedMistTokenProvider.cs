using System.Collections.Concurrent;
using System.Net.Http.Json;
using Cloud.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cloud.Shared.RedMist;

/// <summary>
/// Acquires Keycloak Client Credentials access tokens for each team's RedMist credentials.
/// Caches tokens in memory until 30 s before expiry. Thread-safe.
/// </summary>
public sealed class RedMistTokenProvider : IRedMistTokenProvider
{
    private readonly IDbContextFactory<RaceManagementContext> dbFactory;
    private readonly IHttpClientFactory httpFactory;
    private readonly RedMistOptions options;
    private readonly TimeProvider time;
    private readonly ILogger<RedMistTokenProvider> logger;
    private readonly ConcurrentDictionary<int, CachedToken> cache = new();

    public RedMistTokenProvider(
        IDbContextFactory<RaceManagementContext> dbFactory,
        IHttpClientFactory httpFactory,
        IOptions<RedMistOptions> options,
        TimeProvider time,
        ILogger<RedMistTokenProvider> logger)
    {
        this.dbFactory = dbFactory;
        this.httpFactory = httpFactory;
        this.options = options.Value;
        this.time = time;
        this.logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(int teamId, CancellationToken ct)
    {
        if (cache.TryGetValue(teamId, out var cached) && cached.ExpiresAtUtc > time.GetUtcNow().AddSeconds(30))
            return cached.AccessToken;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var settings = await db.SiteSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TeamId == teamId, ct);
        if (settings is null || string.IsNullOrWhiteSpace(settings.RedMistClientId) || string.IsNullOrWhiteSpace(settings.RedMistClientSecret))
            throw new RedMistAuthException(teamId, "RedMist credentials are not configured for this team.");

        var tokenUrl = $"{options.AuthServerUrl.TrimEnd('/')}/realms/{options.Realm}/protocol/openid-connect/token";
        using var http = httpFactory.CreateClient("redmist-auth");
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = settings.RedMistClientId,
                ["client_secret"] = settings.RedMistClientSecret,
            }),
        };

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            cache.TryRemove(teamId, out _);
            logger.LogWarning("RedMist Keycloak token request failed for team {TeamId}: HTTP {Status} {Body}", teamId, (int)response.StatusCode, body);
            throw new RedMistAuthException(teamId, $"RedMist Keycloak token request failed: HTTP {(int)response.StatusCode}");
        }

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
        if (payload is null || string.IsNullOrEmpty(payload.AccessToken))
            throw new RedMistAuthException(teamId, "RedMist Keycloak returned an empty token payload.");

        var expiresAt = time.GetUtcNow().AddSeconds(payload.ExpiresIn).UtcDateTime;
        cache[teamId] = new CachedToken(payload.AccessToken, expiresAt);
        return payload.AccessToken;
    }

    public void InvalidateToken(int teamId)
    {
        cache.TryRemove(teamId, out _);
    }

    private sealed record CachedToken(string AccessToken, DateTime ExpiresAtUtc);

    private sealed class TokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
