using System.Text.Json;
using System.Text.RegularExpressions;
using Cloud.Shared;
using Cloud.Shared.Alarms;
using Cloud.Shared.Hubs;
using Cloud.Shared.RedMist;
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
/// Subscribes to four pub/sub patterns:
/// - <c>car-channel-changes:*</c> — per-channel-value changes → <see cref="IWebHubClient.ChannelValueChanged"/>
/// - <c>alarm-changes:*</c> — alarm edge/ack notifications → <see cref="IWebHubClient.AlarmChanged"/>
/// - <c>race-state-changes:*</c> — RedMist race-header state → <see cref="IWebHubClient.RaceStateChanged"/>
/// - <c>car-connection-changes:*</c> — CarHub connect/disconnect → <see cref="IWebHubClient.CarConnectionChanged"/>
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
    private static readonly Regex RaceStateTeamIdRegex = BuildRaceStateTeamIdRegex();
    private static readonly Regex CarConnectionTeamIdRegex = BuildCarConnectionTeamIdRegex();
    private static readonly JsonSerializerOptions RaceStateJsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = redis.GetSubscriber();
        var channelPattern = RedisChannel.Pattern(string.Format(Consts.CAR_CHANNEL_CHANGES_CHANNEL, "*"));
        var alarmPattern = RedisChannel.Pattern(string.Format(Consts.ALARM_CHANGES_CHANNEL, "*"));
        var raceStatePattern = RedisChannel.Pattern(string.Format(Consts.RACE_STATE_CHANGES_CHANNEL, "*"));
        var carConnectionPattern = RedisChannel.Pattern(string.Format(Consts.CAR_CONNECTION_CHANGES_CHANNEL, "*"));

        await sub.SubscribeAsync(channelPattern, OnChannelChange);
        await sub.SubscribeAsync(alarmPattern, OnAlarmChange);
        await sub.SubscribeAsync(raceStatePattern, OnRaceStateChange);
        await sub.SubscribeAsync(carConnectionPattern, OnCarConnectionChange);
        logger.LogInformation("ChannelPropagatorService subscribed to patterns '{ChannelPattern}', '{AlarmPattern}', '{RaceStatePattern}', '{CarConnectionPattern}'", channelPattern, alarmPattern, raceStatePattern, carConnectionPattern);

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
            await sub.UnsubscribeAsync(raceStatePattern);
            await sub.UnsubscribeAsync(carConnectionPattern);
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

    private void OnRaceStateChange(RedisChannel channel, RedisValue value)
    {
        try
        {
            var match = RaceStateTeamIdRegex.Match(channel.ToString());
            if (!match.Success || !int.TryParse(match.Groups[1].ValueSpan, out var teamId))
            {
                logger.LogWarning("Unparseable race-state-changes channel '{Channel}'", channel);
                return;
            }

            // An empty payload signals "clear" — sent on detach so the UI blanks the header.
            // (StackExchange.Redis rejects truly null values on PUBLISH, so the publisher
            // uses RedisValue.EmptyString as the clear sentinel.)
            RaceStateDto? dto = null;
            if (!value.IsNullOrEmpty)
            {
                try { dto = JsonSerializer.Deserialize<RaceStateDto>(value.ToString(), RaceStateJsonOptions); }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Corrupt race-state JSON on '{Channel}'", channel);
                    return;
                }
            }

            _ = hub.Clients.Group(WebHub.TeamGroup(teamId)).RaceStateChanged(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to forward race-state change from '{Channel}'", channel);
        }
    }

    private void OnCarConnectionChange(RedisChannel channel, RedisValue value)
    {
        try
        {
            var match = CarConnectionTeamIdRegex.Match(channel.ToString());
            if (!match.Success || !int.TryParse(match.Groups[1].ValueSpan, out var teamId))
            {
                logger.LogWarning("Unparseable car-connection-changes channel '{Channel}'", channel);
                return;
            }

            if (!value.HasValue) return;

            var notification = MessagePackSerializer.Deserialize<CarConnectionChangeNotification>((byte[])value!);
            _ = hub.Clients.Group(WebHub.TeamGroup(teamId)).CarConnectionChanged(notification);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to forward car-connection change from '{Channel}'", channel);
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

    [GeneratedRegex(@"^race-state-changes:(\d+)$", RegexOptions.Compiled)]
    private static partial Regex BuildRaceStateTeamIdRegex();

    [GeneratedRegex(@"^car-connection-changes:(\d+)$", RegexOptions.Compiled)]
    private static partial Regex BuildCarConnectionTeamIdRegex();
}
