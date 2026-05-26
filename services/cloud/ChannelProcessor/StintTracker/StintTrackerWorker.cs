using System.Globalization;
using System.Text.RegularExpressions;
using Channels;
using Cloud.Shared;
using Cloud.Shared.Streaming;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ChannelProcessor.StintTracker;

/// <summary>
/// Derives <c>CurrentStintMinutes</c> and <c>StintCount</c> from <c>InPit</c> channel
/// transitions. Independent of RedMist by design — sees only the channel pipeline
/// (whatever producer wrote InPit). Two concurrent loops:
///
/// - <b>Stream consumer</b>: reads <c>car-channel-values</c> under the
///   <c>channelproc-stint</c> consumer group; detects <c>InPit</c> edges; updates per-car
///   Redis state; emits <c>CurrentStintMinutes</c> and <c>StintCount</c> on transitions.
/// - <b>Heartbeat</b>: every <see cref="StintTrackerConsts.HeartbeatInterval"/>, SCANs
///   per-car state and emits <c>CurrentStintMinutes</c> for cars still on track so the
///   value reflects the increasing stint duration without depending on new InPit messages.
/// </summary>
public sealed partial class StintTrackerWorker : BackgroundService
{
    private static readonly string ConsumerName = Environment.MachineName;
    private const int BatchSize = 50;
    private static readonly TimeSpan IdlePoll = TimeSpan.FromMilliseconds(200);

    private readonly IConnectionMultiplexer redis;
    private readonly ICarChannelDefinitionResolver resolver;
    private readonly ICarChannelPublisher publisher;
    private readonly TimeProvider time;
    private readonly ILogger<StintTrackerWorker> logger;

    public StintTrackerWorker(
        IConnectionMultiplexer redis,
        ICarChannelDefinitionResolver resolver,
        ICarChannelPublisher publisher,
        TimeProvider time,
        ILogger<StintTrackerWorker> logger)
    {
        this.redis = redis;
        this.resolver = resolver;
        this.publisher = publisher;
        this.time = time;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync();
        logger.LogInformation("StintTrackerWorker started (consumer: {Consumer}, group: {Group})",
            ConsumerName, StintTrackerConsts.CONSUMER_GROUP);

        var streamTask = RunStreamLoopAsync(stoppingToken);
        var heartbeatTask = RunHeartbeatLoopAsync(stoppingToken);
        await Task.WhenAll(streamTask, heartbeatTask);
        logger.LogInformation("StintTrackerWorker stopped");
    }

    private async Task EnsureConsumerGroupAsync()
    {
        var db = redis.GetDatabase();
        try
        {
            await db.StreamCreateConsumerGroupAsync(
                Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
                StintTrackerConsts.CONSUMER_GROUP,
                StreamPosition.NewMessages,
                createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP"))
        {
            // Already exists — fine.
        }
    }

    private async Task RunStreamLoopAsync(CancellationToken ct)
    {
        var db = redis.GetDatabase();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var entries = await db.StreamReadGroupAsync(
                    Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
                    StintTrackerConsts.CONSUMER_GROUP,
                    ConsumerName,
                    ">",
                    count: BatchSize);

                if (entries.Length == 0)
                {
                    await Task.Delay(IdlePoll, ct);
                    continue;
                }

                foreach (var entry in entries)
                {
                    await ProcessEntryAsync(entry, ct);
                    await db.StreamAcknowledgeAsync(
                        Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
                        StintTrackerConsts.CONSUMER_GROUP,
                        entry.Id);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "StintTracker stream loop error; retrying");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task ProcessEntryAsync(StreamEntry entry, CancellationToken ct)
    {
        foreach (var field in entry.Values)
        {
            var carKey = field.Name.ToString();
            if (!TryParseCarKey(carKey, out var teamId, out var carNumber)) continue;
            if (!field.Value.HasValue) continue;

            ChannelValue[] values;
            try
            {
                values = MessagePackSerializer.Deserialize<ChannelValue[]>((byte[])field.Value!, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "StintTracker failed to deserialize ChannelValue[] for {CarKey}", carKey);
                continue;
            }

            var sessionMap = await resolver.GetSessionIndexMapAsync(carKey, ct);
            if (sessionMap is null) continue;

            var inPit = ExtractInPit(values, sessionMap);
            if (inPit is null) continue;

            await ApplyInPitAsync(teamId, carNumber, inPit.Value, ct);
        }
    }

    private static (bool Value, DateTime TimestampUtc)? ExtractInPit(
        ChannelValue[] values,
        IReadOnlyDictionary<ushort, ChannelDefinition> sessionMap)
    {
        (bool Value, DateTime TimestampUtc)? latest = null;
        for (var i = 0; i < values.Length; i++)
        {
            var cv = values[i];
            if (!sessionMap.TryGetValue(cv.SessionIndex, out var def)) continue;
            if (def.Id != StintReservedChannelGuids.InPit) continue;

            var sample = (Value: cv.GetValueDouble() > 0.5, TimestampUtc: cv.Timestamp);
            if (latest is null || sample.TimestampUtc >= latest.Value.TimestampUtc)
                latest = sample;
        }
        return latest;
    }

    private async Task ApplyInPitAsync(int teamId, string carNumber, (bool Value, DateTime TimestampUtc) sample, CancellationToken ct)
    {
        var state = await ReadStateAsync(teamId, carNumber, ct) ?? new StintTrackerState();
        var result = StintTrackerLogic.ApplyInPit(state, sample.Value, sample.TimestampUtc);

        switch (result.Decision)
        {
            case InPitDecision.PitIn:
                logger.LogInformation("StintTracker: team {TeamId} car {CarNumber} pit-in (stint #{Count})",
                    teamId, carNumber, result.NewState.StintCount);
                break;
            case InPitDecision.PitOut:
                logger.LogInformation("StintTracker: team {TeamId} car {CarNumber} pit-out — stint {Count} starting",
                    teamId, carNumber, result.NewState.StintCount + 1);
                break;
        }

        if (result.ShouldEmit)
            await EmitAndSaveAsync(teamId, carNumber, result.NewState, sample.TimestampUtc, ct);
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var server = redis.GetServer(redis.GetEndPoints()[0]);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var nowUtc = time.GetUtcNow().UtcDateTime;
                await foreach (var key in server.KeysAsync(pattern: StintTrackerConsts.STATE_KEY_SCAN_PATTERN).WithCancellation(ct))
                {
                    var keyStr = key.ToString();
                    if (!TryParseStateKey(keyStr, out var teamId, out var carNumber)) continue;

                    var state = await ReadStateAsync(teamId, carNumber, ct);
                    if (state is null) continue;
                    if (!StintTrackerLogic.ShouldHeartbeat(state, nowUtc, StintTrackerConsts.HeartbeatInterval)) continue;

                    await EmitAndSaveAsync(teamId, carNumber, state, nowUtc, ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "StintTracker heartbeat loop error; retrying");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(15), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }

    private async Task EmitAndSaveAsync(int teamId, string carNumber, StintTrackerState state, DateTime nowUtc, CancellationToken ct)
    {
        var (minutes, count) = StintTrackerLogic.ComputeEmittedValues(state, nowUtc);
        var publishes = new[]
        {
            new PublishedChannelValue(
                StintReservedChannelGuids.CurrentStintMinutes,
                minutes.ToString("F1", CultureInfo.InvariantCulture),
                nowUtc),
            new PublishedChannelValue(
                StintReservedChannelGuids.StintCount,
                count.ToString(CultureInfo.InvariantCulture),
                nowUtc),
        };

        await publisher.PublishAsync(teamId, carNumber, publishes, ct);

        state.LastEmittedAtUtc = nowUtc;
        await WriteStateAsync(teamId, carNumber, state, ct);
    }

    private async Task<StintTrackerState?> ReadStateAsync(int teamId, string carNumber, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var key = string.Format(StintTrackerConsts.STATE_KEY, teamId, carNumber);
        var bytes = await db.StringGetAsync(key);
        if (bytes.IsNullOrEmpty) return null;
        try
        {
            return MessagePackSerializer.Deserialize<StintTrackerState>((byte[])bytes!, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "StintTracker: corrupt state for team {TeamId} car {CarNumber}; resetting", teamId, carNumber);
            return null;
        }
    }

    private async Task WriteStateAsync(int teamId, string carNumber, StintTrackerState state, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var key = string.Format(StintTrackerConsts.STATE_KEY, teamId, carNumber);
        var bytes = MessagePackSerializer.Serialize(state, cancellationToken: ct);
        await db.StringSetAsync(key, bytes, StintTrackerConsts.StateTtl);
    }

    private static bool TryParseCarKey(string carKey, out int teamId, out string carNumber)
    {
        var match = CarKeyRegex().Match(carKey);
        if (!match.Success) { teamId = 0; carNumber = string.Empty; return false; }
        teamId = int.Parse(match.Groups[1].ValueSpan);
        carNumber = match.Groups[2].Value;
        return true;
    }

    private static bool TryParseStateKey(string key, out int teamId, out string carNumber)
    {
        var match = StateKeyRegex().Match(key);
        if (!match.Success) { teamId = 0; carNumber = string.Empty; return false; }
        teamId = int.Parse(match.Groups[1].ValueSpan);
        carNumber = match.Groups[2].Value;
        return true;
    }

    [GeneratedRegex(@"^team-(\d+)-car-(.+)$", RegexOptions.Compiled)]
    private static partial Regex CarKeyRegex();

    [GeneratedRegex(@"^stint-state:(\d+):(.+)$", RegexOptions.Compiled)]
    private static partial Regex StateKeyRegex();
}
