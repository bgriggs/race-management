using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Common;

namespace RaceManagementService.Cloud;

public class CloudConfigurationClient(HttpClient httpClient, ILogger<CloudConfigurationClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<bool> SaveCarConfigurationAsync(
        int teamId,
        CarConfiguration configuration,
        string jwt,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1.0/configuration/save-car-configuration?teamId={teamId}")
        {
            Content = JsonContent.Create(configuration, options: JsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        try
        {
            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Cloud save of configuration {ConfigurationId} returned {StatusCode}.",
                    configuration.ConfigurationId,
                    (int)response.StatusCode);
                return false;
            }

            logger.LogInformation(
                "Successfully forwarded configuration {ConfigurationId} to cloud for team {TeamId}.",
                configuration.ConfigurationId,
                teamId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to forward configuration {ConfigurationId} to cloud.",
                configuration.ConfigurationId);
            return false;
        }
    }

    public async Task<IReadOnlyList<UserTeam>?> ListMyTeamsAsync(string jwt, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "v1.0/configuration/list-my-teams");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        try
        {
            using var response = await httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Cloud list-my-teams returned {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            var teams = await response.Content.ReadFromJsonAsync<List<UserTeam>>(JsonOptions, ct);
            return teams ?? new List<UserTeam>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load user teams from cloud.");
            return null;
        }
    }
}
