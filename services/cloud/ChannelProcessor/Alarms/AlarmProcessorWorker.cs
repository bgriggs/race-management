using System.Text.RegularExpressions;
using ChannelProcessor.Alarms.Channel;
using ChannelProcessor.Alarms.Config;
using ChannelProcessor.Alarms.Persistence;
using ChannelProcessor.Alarms.State;
using Channels;
using Channels.Alarms;
using Channels.Logic;
using Cloud.Shared;
using Cloud.Shared.Alarms;
using Cloud.Shared.Streaming;
using StackExchange.Redis;

namespace ChannelProcessor.Alarms;

/// <summary>
/// Fifth ChannelProcessor worker — the Alarm Evaluator. Subscribes to the
/// <c>car-channel-values</c> stream under its own consumer group so it sees every
/// message independently of the state-cache, fuel, and other consumers. For each
/// message: parses the carKey, loads the effective <see cref="AlarmDefinition"/> set
/// (team-level ∪ car-level), builds car-scoped repositories, invokes
/// <see cref="AlarmEvaluation.UpdateAlarmsAsync"/>, and reflects the resulting
/// pre/post snapshots into Postgres via <see cref="ActiveAlarmStore"/> before ACKing.
/// </summary>
public sealed partial class AlarmProcessorWorker(
    IConnectionMultiplexer redis,
    ICarChannelDefinitionResolver channelResolver,
    IAlarmDefinitionRepository alarmDefinitions,
    ActiveAlarmStore activeAlarmStore,
    IRedisAlarmStateGateway alarmStateGateway,
    ICarChannelPublisher carPublisher,
    ITeamChannelPublisher teamPublisher,
    TimeProvider timeProvider,
    ILogger<AlarmProcessorWorker> logger) : BackgroundService
{
    private static readonly string _consumerName = Environment.MachineName;
    private const int BatchSize = 50;
    private const int IdlePollMs = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync();

        logger.LogInformation("AlarmProcessorWorker started (consumer: {Consumer}, group: {Group})",
            _consumerName, Consts.CHANNEL_PROC_ALARM_CONSUMER_GROUP);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var db = redis.GetDatabase();
                var entries = await db.StreamReadGroupAsync(
                    Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
                    Consts.CHANNEL_PROC_ALARM_CONSUMER_GROUP,
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

                    // ACK only after all evaluator state writes + Postgres edge writes
                    // are complete (ADR-0002 ordering rule).
                    await db.StreamAcknowledgeAsync(
                        Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
                        Consts.CHANNEL_PROC_ALARM_CONSUMER_GROUP,
                        entry.Id);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in AlarmProcessorWorker loop; will retry");
                await Task.Delay(1000, stoppingToken);
            }
        }

        logger.LogInformation("AlarmProcessorWorker stopped");
    }

    private async Task ProcessEntryAsync(StreamEntry entry, CancellationToken ct)
    {
        foreach (var field in entry.Values)
        {
            var carKey = field.Name.ToString();
            if (!TryParseCarKey(carKey, out var teamId, out var carNumber))
            {
                logger.LogWarning("Skipping alarm-stream entry {EntryId}: unrecognized carKey {CarKey}", entry.Id, carKey);
                continue;
            }

            await EvaluateCarAsync(teamId, carNumber, carKey, ct);
        }
    }

    private async Task EvaluateCarAsync(int teamId, string carNumber, string carKey, CancellationToken ct)
    {
        var definitions = await alarmDefinitions.GetForCarAsync(teamId, carNumber, ct);
        if (definitions.Count == 0) return;

        var sessionMap = await channelResolver.GetSessionIndexMapAsync(carKey, ct);
        if (sessionMap is null) return; // no active config — cannot resolve channel scope
        var channelIdMap = await channelResolver.GetChannelIdMapAsync(carKey, ct) ?? new Dictionary<Guid, ushort>();

        var definitionsById = BuildDefinitionsById(sessionMap);

        var channelDefRepo = new CarScopedChannelDefinitionRepository(sessionMap);
        var channelRepo = new CarScopedChannelRepository(
            teamId, carNumber, carKey, definitionsById, channelIdMap,
            redis, carPublisher, teamPublisher, timeProvider, logger, ct);

        var alarmRepo = new RedisAlarmRepository(alarmStateGateway, carKey, definitions);
        var statementStateRepo = new RedisStatementStateRepository(redis, carKey);
        var comparisonDurationRepo = new RedisComparisonDurationRepository(redis, carKey);
        var previousValueRepo = new RedisPreviousChannelValueRepository(redis, carKey);

        var evaluation = new AlarmEvaluation(
            alarmRepo,
            channelRepo,
            channelDefRepo,
            statementStateRepo,
            comparisonDurationRepo,
            previousValueRepo,
            timeProvider);

        await evaluation.UpdateAlarmsAsync();

        var records = alarmRepo.GetTickRecords().ToList();
        var tickTs = timeProvider.GetUtcNow().UtcDateTime;
        await activeAlarmStore.RecordTickAsync(teamId, carNumber, records, tickTs, ct);
    }

    private static Dictionary<Guid, ChannelDefinition> BuildDefinitionsById(IReadOnlyDictionary<ushort, ChannelDefinition> sessionMap)
    {
        var byId = new Dictionary<Guid, ChannelDefinition>(sessionMap.Count);
        foreach (var def in sessionMap.Values)
        {
            byId[def.Id] = def;
        }
        return byId;
    }

    private async Task EnsureConsumerGroupAsync()
    {
        var db = redis.GetDatabase();
        try
        {
            await db.StreamCreateConsumerGroupAsync(
                Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
                Consts.CHANNEL_PROC_ALARM_CONSUMER_GROUP,
                StreamPosition.NewMessages,
                createStream: true);
            logger.LogInformation("Created consumer group '{Group}' on stream '{Stream}'",
                Consts.CHANNEL_PROC_ALARM_CONSUMER_GROUP, Consts.CAR_CHANNEL_VALUES_STREAM_KEY);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP"))
        {
            logger.LogDebug("Consumer group '{Group}' already exists", Consts.CHANNEL_PROC_ALARM_CONSUMER_GROUP);
        }
    }

    private static bool TryParseCarKey(string carKey, out int teamId, out string carNumber)
    {
        var match = CarKeyRegex().Match(carKey);
        if (!match.Success)
        {
            teamId = 0;
            carNumber = string.Empty;
            return false;
        }
        teamId = int.Parse(match.Groups[1].ValueSpan);
        carNumber = match.Groups[2].Value;
        return true;
    }

    [GeneratedRegex(@"^team-(\d+)-car-(.+)$", RegexOptions.Compiled)]
    private static partial Regex CarKeyRegex();
}
