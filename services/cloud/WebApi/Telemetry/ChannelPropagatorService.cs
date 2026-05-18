using System.Text.RegularExpressions;
using Cloud.Shared;
using Cloud.Shared.Hubs;
using Cloud.Shared.Telemetry;
using MessagePack;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace WebApi.Telemetry;

/// <summary>
/// Bridges ChannelProcessor's Redis pub/sub channel-change notifications into the
/// <see cref="WebHub"/> SignalR groups for each team, and pushes a periodic full
/// snapshot to every team with at least one active connection.
///
/// Note: when WebApi runs multiple replicas, every replica subscribes to the same
/// pub/sub pattern and forwards into the SignalR backplane, so clients would
/// receive each change message N times. Deploy as a single replica for now, or
/// add leader election before scaling out.
/// </summary>
public partial class ChannelPropagatorService(
    IConnectionMultiplexer redis,
    IHubContext<WebHub, IWebHubClient> hub,
    IConnectedTeamsTracker teamsTracker,
    ITeamChannelSnapshotService snapshotService,
    ILogger<ChannelPropagatorService> logger) : BackgroundService
{
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMilliseconds(2500);
    private static readonly Regex CarKeyRegex = BuildCarKeyRegex();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = redis.GetSubscriber();
        var pattern = RedisChannel.Pattern(string.Format(Consts.CAR_CHANNEL_CHANGES_CHANNEL, "*"));

        await sub.SubscribeAsync(pattern, OnChannelChange);
        logger.LogInformation("ChannelPropagatorService subscribed to pattern '{Pattern}'", pattern);

        try
        {
            using var timer = new PeriodicTimer(SnapshotInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await BroadcastSnapshotsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            await sub.UnsubscribeAsync(pattern);
            logger.LogInformation("ChannelPropagatorService stopped");
        }
    }

    private void OnChannelChange(RedisChannel channel, RedisValue value)
    {
        try
        {
            var channelName = channel.ToString();
            var prefix = string.Format(Consts.CAR_CHANNEL_CHANGES_CHANNEL, string.Empty);
            if (!channelName.StartsWith(prefix)) return;

            var carKey = channelName[prefix.Length..];
            var match = CarKeyRegex.Match(carKey);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out var teamId))
            {
                logger.LogWarning("Unparseable carKey '{CarKey}' on channel '{Channel}'", carKey, channelName);
                return;
            }

            if (!value.HasValue) return;

            var notification = MessagePackSerializer.Deserialize<ChannelChangeNotification>((byte[])value!);

            // Fire-and-forget: the subscription callback is sync; we don't want to block the Redis dispatcher.
            _ = hub.Clients.Group(WebHub.TeamGroup(teamId))
                .ChannelValueChanged(carKey, notification);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to forward channel change from '{Channel}'", channel);
        }
    }

    private async Task BroadcastSnapshotsAsync(CancellationToken ct)
    {
        var teams = teamsTracker.GetConnectedTeams();
        if (teams.Count == 0) return;

        foreach (var teamId in teams)
        {
            try
            {
                var snapshot = await snapshotService.GetTeamSnapshotAsync(teamId, ct);
                await hub.Clients.Group(WebHub.TeamGroup(teamId)).ChannelSnapshot(snapshot);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed periodic snapshot broadcast for team {TeamId}", teamId);
            }
        }
    }

    [GeneratedRegex(@"^team-(\d+)-car-(.+)$", RegexOptions.Compiled)]
    private static partial Regex BuildCarKeyRegex();
}
