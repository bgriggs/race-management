using System.Text.RegularExpressions;
using Cloud.Shared;
using StackExchange.Redis;

namespace ChannelProcessor.Alarms.Config;

/// <summary>
/// Subscribes to <c>alarm-config-changed:*</c> Redis pub/sub and invalidates the
/// affected team's cached alarm-definition sets on receipt. Published by WebApi after
/// a definition save or delete; replaces the 2-minute TTL safety net for UI edits.
/// </summary>
public sealed partial class AlarmConfigChangeListener(
    IConnectionMultiplexer redis,
    IAlarmDefinitionRepository repository,
    ILogger<AlarmConfigChangeListener> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sub = redis.GetSubscriber();
        var pattern = RedisChannel.Pattern(string.Format(Consts.ALARM_CONFIG_CHANGED_CHANNEL, "*"));

        await sub.SubscribeAsync(pattern, OnConfigChanged);
        logger.LogInformation("AlarmConfigChangeListener subscribed to pattern '{Pattern}'", pattern);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            await sub.UnsubscribeAsync(pattern);
            logger.LogInformation("AlarmConfigChangeListener stopped");
        }
    }

    private void OnConfigChanged(RedisChannel channel, RedisValue value)
    {
        try
        {
            var match = TeamIdRegex().Match(channel.ToString());
            if (!match.Success || !int.TryParse(match.Groups[1].ValueSpan, out var teamId))
            {
                logger.LogWarning("Unparseable alarm-config-changed channel '{Channel}'", channel);
                return;
            }

            // Fire-and-forget: the subscription callback is sync; we don't want to block
            // the Redis dispatcher on a cache eviction.
            _ = repository.InvalidateTeamAsync(teamId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to invalidate alarm definitions from '{Channel}'", channel);
        }
    }

    [GeneratedRegex(@"^alarm-config-changed:(\d+)$", RegexOptions.Compiled)]
    private static partial Regex TeamIdRegex();
}
