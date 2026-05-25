using System.Text.RegularExpressions;
using Cloud.Shared;
using Cloud.Shared.Alarms;
using Cloud.Shared.Hubs;
using Cloud.Shared.Telemetry;
using MessagePack;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace WebApi.Telemetry;

/// <summary>
/// Bridges ChannelProcessor's Redis pub/sub change notifications into the
/// <see cref="WebHub"/> SignalR groups for each team, and pushes a periodic full
/// snapshot (channels + active alarms) to every team with at least one active
/// connection.
///
/// Subscribes to two pub/sub patterns:
/// - <c>car-channel-changes:*</c> — per-channel-value changes → <see cref="IWebHubClient.ChannelValueChanged"/>
/// - <c>alarm-changes:*</c> — alarm edge/ack notifications → <see cref="IWebHubClient.AlarmChanged"/>
///
/// Note: when WebApi runs multiple replicas, every replica subscribes to the same
/// pub/sub patterns and forwards into the SignalR backplane, so clients would
/// receive each change message N times. Deploy as a single replica for now, or
/// add leader election before scaling out.
/// </summary>
public partial class ChannelPropagatorService(
    IConnectionMultiplexer redis,
    IHubContext<WebHub, IWebHubClient> hub,
    IConnectedTeamsTracker teamsTracker,
    ITeamChannelSnapshotService snapshotService,
    IActiveAlarmsReader activeAlarmsReader,
    ILogger<ChannelPropagatorService> logger) : BackgroundService
{
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromMilliseconds(2500);
    private static readonly Regex CarKeyRegex = BuildCarKeyRegex();
    private static readonly Regex AlarmTeamIdRegex = BuildAlarmTeamIdRegex();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = redis.GetSubscriber();
        var channelPattern = RedisChannel.Pattern(string.Format(Consts.CAR_CHANNEL_CHANGES_CHANNEL, "*"));
        var alarmPattern = RedisChannel.Pattern(string.Format(Consts.ALARM_CHANGES_CHANNEL, "*"));

        await sub.SubscribeAsync(channelPattern, OnChannelChange);
        await sub.SubscribeAsync(alarmPattern, OnAlarmChange);
        logger.LogInformation("ChannelPropagatorService subscribed to patterns '{ChannelPattern}', '{AlarmPattern}'", channelPattern, alarmPattern);

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
            await sub.UnsubscribeAsync(channelPattern);
            await sub.UnsubscribeAsync(alarmPattern);
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

    private void OnAlarmChange(RedisChannel channel, RedisValue value)
    {
        try
        {
            var match = AlarmTeamIdRegex.Match(channel.ToString());
            if (!match.Success || !int.TryParse(match.Groups[1].ValueSpan, out var teamId))
            {
                logger.LogWarning("Unparseable alarm-changes channel '{Channel}'", channel);
                return;
            }

            if (!value.HasValue) return;

            var notification = MessagePackSerializer.Deserialize<AlarmChangeNotification>((byte[])value!);
            _ = hub.Clients.Group(WebHub.TeamGroup(teamId)).AlarmChanged(notification);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to forward alarm change from '{Channel}'", channel);
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

                var alarms = await activeAlarmsReader.GetForTeamAsync(teamId, includeAcknowledged: false, ct);
                await hub.Clients.Group(WebHub.TeamGroup(teamId)).AlarmSnapshot(alarms);
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

    [GeneratedRegex(@"^alarm-changes:(\d+)$", RegexOptions.Compiled)]
    private static partial Regex BuildAlarmTeamIdRegex();
}
