using Channels;
using Cloud.Shared;
using Cloud.Shared.Telemetry;
using MessagePack;
using StackExchange.Redis;

namespace ChannelProcessor.Telemetry;

/// <summary>
/// Consumes the <c>team-channel-values</c> Redis Stream, storing the latest value for
/// each PerTeam channel per team and publishing a pub/sub change notification when a
/// value differs from the previously stored one. Parallel to <see cref="TelemetryStreamConsumer"/>
/// but operates on the team-scoped stream/state, keyed by stable ChannelId Guid rather
/// than per-car SessionIndex.
/// </summary>
public class TeamChannelStreamConsumer(
    IConnectionMultiplexer redis,
    ITeamChannelStateRepository teamState,
    ILogger<TeamChannelStreamConsumer> logger) : BackgroundService
{
    private static readonly string _consumerName = Environment.MachineName;
    private const int BatchSize = 50;
    private const int IdlePollMs = 50;
    private const string TeamFieldPrefix = "team-";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync();

        logger.LogInformation("TeamChannelStreamConsumer started (consumer: {Consumer}, group: {Group})",
            _consumerName, Consts.CHANNEL_PROC_CONSUMER_GROUP);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var db = redis.GetDatabase();
                var entries = await db.StreamReadGroupAsync(
                    Consts.TEAM_CHANNEL_VALUES_STREAM_KEY,
                    Consts.CHANNEL_PROC_CONSUMER_GROUP,
                    _consumerName,
                    ">",
                    count: BatchSize);

                if (entries.Length == 0)
                {
                    await Task.Delay(IdlePollMs, stoppingToken);
                    continue;
                }

                foreach (var entry in entries)
                {
                    await ProcessEntryAsync(entry, stoppingToken);

                    await db.StreamAcknowledgeAsync(
                        Consts.TEAM_CHANNEL_VALUES_STREAM_KEY,
                        Consts.CHANNEL_PROC_CONSUMER_GROUP,
                        entry.Id);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reading from team-channel-values stream; will retry");
                await Task.Delay(1000, stoppingToken);
            }
        }

        logger.LogInformation("TeamChannelStreamConsumer stopped");
    }

    private async Task ProcessEntryAsync(StreamEntry entry, CancellationToken ct)
    {
        foreach (var field in entry.Values)
        {
            var teamField = field.Name.ToString();
            if (!TryParseTeamId(teamField, out var teamId))
            {
                logger.LogWarning("Skipping team stream entry {EntryId}: unrecognized field name {Field}", entry.Id, teamField);
                continue;
            }
            if (!field.Value.HasValue) continue;

            TeamChannelValue[] values;
            try
            {
                values = MessagePackSerializer.Deserialize<TeamChannelValue[]>((byte[])field.Value!, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to deserialize TeamChannelValue[] for team {TeamId}, entry {EntryId}", teamId, entry.Id);
                continue;
            }

            int changedCount = 0;
            foreach (var v in values)
            {
                var snapshot = new ChannelValueSnapshot { Value = v.Value, Timestamp = v.Timestamp };
                var changed = await teamState.SetIfChangedAsync(teamId, v.ChannelId, snapshot, ct);
                if (changed) changedCount++;
            }

            if (changedCount > 0)
                logger.LogDebug("Team {TeamId}: {Changed}/{Total} channels changed", teamId, changedCount, values.Length);
        }
    }

    private static bool TryParseTeamId(string field, out int teamId)
    {
        teamId = 0;
        if (!field.StartsWith(TeamFieldPrefix, StringComparison.Ordinal)) return false;
        return int.TryParse(field.AsSpan(TeamFieldPrefix.Length), out teamId);
    }

    private async Task EnsureConsumerGroupAsync()
    {
        var db = redis.GetDatabase();
        try
        {
            await db.StreamCreateConsumerGroupAsync(
                Consts.TEAM_CHANNEL_VALUES_STREAM_KEY,
                Consts.CHANNEL_PROC_CONSUMER_GROUP,
                StreamPosition.NewMessages,
                createStream: true);
            logger.LogInformation("Created consumer group '{Group}' on stream '{Stream}'",
                Consts.CHANNEL_PROC_CONSUMER_GROUP, Consts.TEAM_CHANNEL_VALUES_STREAM_KEY);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP"))
        {
            logger.LogDebug("Consumer group '{Group}' already exists on stream '{Stream}'",
                Consts.CHANNEL_PROC_CONSUMER_GROUP, Consts.TEAM_CHANNEL_VALUES_STREAM_KEY);
        }
    }
}
