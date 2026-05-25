using System.Security.Claims;
using Cloud.Shared.Alarms;
using Cloud.Shared.Auth;
using Cloud.Shared.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Cloud.Shared.Hubs;

[Authorize]
public class WebHub(
    ILogger<WebHub> logger,
    ITeamRoleContext teamRoleContext,
    IConnectedTeamsTracker teamsTracker,
    ITeamChannelSnapshotService snapshotService,
    IActiveAlarmsReader activeAlarmsReader) : Hub<IWebHubClient>
{
    public const string TeamGroupPrefix = "team-";
    private const string TeamIdItemKey = "teamId";

    public static string TeamGroup(int teamId) => $"{TeamGroupPrefix}{teamId}";

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        logger.LogInformation("WebHub connection {ConnectionId} established; awaiting SubscribeToTeam", Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(TeamIdItemKey, out var raw) && raw is int teamId)
        {
            teamsTracker.Remove(teamId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, TeamGroup(teamId));
            logger.LogInformation("WebHub connection {ConnectionId} disconnected from team {TeamId}", Context.ConnectionId, teamId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribes this connection to a team's broadcast group. Callers must be a
    /// member of the team. Replaces any prior subscription on this connection.
    /// Sends an initial <see cref="IWebHubClient.ChannelSnapshot"/> for the new team.
    /// </summary>
    public async Task SubscribeToTeam(int teamId)
    {
        var email = GetEmail();
        if (string.IsNullOrEmpty(email))
        {
            logger.LogWarning("WebHub SubscribeToTeam: no email claim on connection {ConnectionId}", Context.ConnectionId);
            throw new HubException("Missing email claim.");
        }

        if (!await teamRoleContext.IsUserInTeamAsync(email, teamId, Context.ConnectionAborted))
        {
            logger.LogWarning("WebHub SubscribeToTeam: {Email} not a member of team {TeamId}", email, teamId);
            throw new HubException($"Not a member of team {teamId}.");
        }

        if (Context.Items.TryGetValue(TeamIdItemKey, out var raw) && raw is int prevTeamId)
        {
            if (prevTeamId == teamId)
            {
                // Already subscribed; resend snapshot so the client gets fresh state.
                await SendSnapshotAsync(teamId);
                return;
            }
            teamsTracker.Remove(prevTeamId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, TeamGroup(prevTeamId));
        }

        Context.Items[TeamIdItemKey] = teamId;
        await Groups.AddToGroupAsync(Context.ConnectionId, TeamGroup(teamId));
        teamsTracker.Add(teamId);
        logger.LogInformation("WebHub connection {ConnectionId} subscribed to team {TeamId}", Context.ConnectionId, teamId);

        await SendSnapshotAsync(teamId);
    }

    private async Task SendSnapshotAsync(int teamId)
    {
        try
        {
            var snapshot = await snapshotService.GetTeamSnapshotAsync(teamId, Context.ConnectionAborted);
            await Clients.Caller.ChannelSnapshot(snapshot);

            var alarms = await activeAlarmsReader.GetForTeamAsync(teamId, includeAcknowledged: false, Context.ConnectionAborted);
            await Clients.Caller.AlarmSnapshot(alarms);
        }
        catch (OperationCanceledException) { /* client disconnected before we could send */ }
        catch (Exception ex)
        {
            logger.LogError(ex, "WebHub failed to send snapshot to {ConnectionId} for team {TeamId}", Context.ConnectionId, teamId);
        }
    }

    private string? GetEmail()
    {
        var user = Context.User;
        return user?.FindFirst(ClaimTypes.Email)?.Value
            ?? user?.FindFirst("email")?.Value;
    }
}
