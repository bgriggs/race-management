using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedMist.TimingCommon.Models;

namespace Cloud.Shared.RedMist;

/// <summary>
/// Default <see cref="IRedMistRestClient"/> implementation backed by <see cref="HttpClient"/>.
/// Authentication is via <see cref="IRedMistTokenProvider"/>; one 401 retry per call invalidates
/// the cached token and tries again before surfacing the failure.
/// </summary>
public sealed class RedMistRestClient : IRedMistRestClient
{
    private readonly IHttpClientFactory httpFactory;
    private readonly IRedMistTokenProvider tokenProvider;
    private readonly RedMistOptions options;
    private readonly ILogger<RedMistRestClient> logger;

    public RedMistRestClient(
        IHttpClientFactory httpFactory,
        IRedMistTokenProvider tokenProvider,
        IOptions<RedMistOptions> options,
        ILogger<RedMistRestClient> logger)
    {
        this.httpFactory = httpFactory;
        this.tokenProvider = tokenProvider;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<IReadOnlyList<Event>> LoadEventsAsync(int teamId, DateTime startDateUtc, CancellationToken ct)
    {
        var iso = startDateUtc.ToUniversalTime().ToString("o");
        return await GetJsonAsync<List<Event>>(teamId, $"/v2/Events/LoadEvents?startDateUtc={Uri.EscapeDataString(iso)}", ct) ?? [];
    }

    public Task<Event?> LoadEventAsync(int teamId, int eventId, CancellationToken ct) =>
        GetJsonAsync<Event>(teamId, $"/v2/Events/LoadEvent?eventId={eventId}", ct);

    public async Task<IReadOnlyList<EventListSummary>> LoadLiveEventsAsync(int teamId, CancellationToken ct) =>
        await GetJsonAsync<List<EventListSummary>>(teamId, "/v2/Events/LoadLiveEvents", ct) ?? [];

    public Task<SessionState?> GetCurrentSessionStateAsync(int teamId, int eventId, CancellationToken ct) =>
        GetJsonAsync<SessionState>(teamId, $"/v2/Events/GetCurrentSessionStateJson?eventId={eventId}", ct);

    public async Task<IReadOnlyList<CarPosition>> LoadCarLapsAsync(int teamId, int eventId, int sessionId, string carNumber, CancellationToken ct) =>
        await GetJsonAsync<List<CarPosition>>(teamId, $"/v2/Events/LoadCarLaps?eventId={eventId}&sessionId={sessionId}&carNumber={Uri.EscapeDataString(carNumber)}", ct) ?? [];

    public async Task<IReadOnlyList<CarPosition>> LoadSessionLapsAsync(int teamId, int eventId, int sessionId, CancellationToken ct) =>
        await GetJsonAsync<List<CarPosition>>(teamId, $"/v2/Events/LoadSessionLaps?eventId={eventId}&sessionId={sessionId}", ct) ?? [];

    public async Task<IReadOnlyList<OrganizationSummary>> LoadOrganizationsAsync(int teamId, CancellationToken ct) =>
        await GetJsonAsync<List<OrganizationSummary>>(teamId, "/v1/Organization/GetOrganizations", ct) ?? [];

    private async Task<T?> GetJsonAsync<T>(int teamId, string pathAndQuery, CancellationToken ct)
    {
        var url = options.StatusApiUrl.TrimEnd('/') + pathAndQuery;
        using var http = httpFactory.CreateClient("redmist-api");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await tokenProvider.GetAccessTokenAsync(teamId, ct);
            if (token is null)
            {
                // No credentials configured — treat the same as a 404 / empty result so the
                // read-side surface (UI dropdowns, event pickers) stays quiet for teams that
                // haven't opted into RedMist.
                return default;
            }
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Clear();
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
            {
                logger.LogInformation("RedMist returned 401 for team {TeamId}; invalidating token and retrying once.", teamId);
                tokenProvider.InvalidateToken(teamId);
                continue;
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
                return default;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(ct);
        }

        // Unreachable: the loop either returns, throws via EnsureSuccessStatusCode, or completes the retry path.
        throw new InvalidOperationException("RedMist request retry exhausted unexpectedly.");
    }
}
