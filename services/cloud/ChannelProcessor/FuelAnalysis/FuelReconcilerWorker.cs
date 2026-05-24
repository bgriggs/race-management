using System.Text.RegularExpressions;
using ChannelProcessor.FuelAnalysis.ChannelInput;
using ChannelProcessor.FuelAnalysis.Estimators;
using ChannelProcessor.FuelAnalysis.Pace;
using ChannelProcessor.FuelAnalysis.Refuel;
using ChannelProcessor.FuelAnalysis.Session;
using ChannelProcessor.FuelAnalysis.Snapshot;
using ChannelProcessor.FuelAnalysis.State;
using ChannelProcessor.FuelAnalysis.Windows;
using Channels;
using Cloud.Shared;
using Cloud.Shared.Database;
using Cloud.Shared.Streaming;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace ChannelProcessor.FuelAnalysis;

/// <summary>
/// Fourth ChannelProcessor worker — the Fuel Reconciler. Consumes the
/// <c>car-channel-values</c> stream under its own consumer group so it sees every
/// message independently of the latest-state cache consumer. For each message:
/// gates on an active <see cref="Cloud.Shared.Database.Models.Race"/> (design.md §616),
/// initializes the per-car session on first sight, applies stint-boundary updates from
/// <c>InPit</c> transitions, runs refuel-anchor detection, persists any new
/// <see cref="Cloud.Shared.Database.Models.FuelAnalysis.RefuelEvent"/> /
/// <see cref="Cloud.Shared.Database.Models.FuelAnalysis.FuelWindow"/> rows, and lazily
/// commits the per-window ECU reset verdict once its deadline elapses.
/// </summary>
public sealed partial class FuelReconcilerWorker(
    IConnectionMultiplexer redis,
    IDbContextFactory<RaceManagementContext> dbFactory,
    ICarChannelDefinitionResolver channelResolver,
    IRaceSessionGate sessionGate,
    ICarFuelStateRepository stateRepository,
    SessionLifecycleHandler sessionLifecycle,
    StintLifecycle stintLifecycle,
    DriverPaceTracker paceTracker,
    ThrottleProxyTracker throttleProxyTracker,
    RefuelEventDetector refuelDetector,
    FuelWindowLifecycle fuelWindowLifecycle,
    ManualEntryHandler manualEntryHandler,
    EcuResetClassifier ecuResetClassifier,
    SnapshotEmitter snapshotEmitter,
    TimeProvider timeProvider,
    ILogger<FuelReconcilerWorker> logger) : BackgroundService
{
    private static readonly string _consumerName = Environment.MachineName;
    private const int BatchSize = 50;
    private const int IdlePollMs = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync();

        logger.LogInformation("FuelReconcilerWorker started (consumer: {Consumer}, group: {Group})",
            _consumerName, Consts.CHANNEL_PROC_FUEL_CONSUMER_GROUP);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var db = redis.GetDatabase();
                var entries = await db.StreamReadGroupAsync(
                    Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
                    Consts.CHANNEL_PROC_FUEL_CONSUMER_GROUP,
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

                    // ACK only after Postgres + Redis state writes are complete (ADR-0002).
                    await db.StreamAcknowledgeAsync(
                        Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
                        Consts.CHANNEL_PROC_FUEL_CONSUMER_GROUP,
                        entry.Id);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in FuelReconcilerWorker loop; will retry");
                await Task.Delay(1000, stoppingToken);
            }
        }

        logger.LogInformation("FuelReconcilerWorker stopped");
    }

    private async Task ProcessEntryAsync(StreamEntry entry, CancellationToken ct)
    {
        foreach (var field in entry.Values)
        {
            var carKey = field.Name.ToString();
            if (!TryParseCarKey(carKey, out var teamId, out var carNumber))
            {
                logger.LogWarning("Skipping fuel-stream entry {EntryId}: unrecognized carKey {CarKey}", entry.Id, carKey);
                continue;
            }
            if (!field.Value.HasValue) continue;

            var activeRaceId = await sessionGate.GetActiveRaceIdAsync(teamId, ct);
            if (activeRaceId is null) continue;

            ChannelValue[] channelValues;
            try
            {
                channelValues = MessagePackSerializer.Deserialize<ChannelValue[]>((byte[])field.Value!, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to deserialize ChannelValue[] for car {CarKey}, entry {EntryId}", carKey, entry.Id);
                continue;
            }

            var sessionMap = await channelResolver.GetSessionIndexMapAsync(carKey, ct);
            if (sessionMap is null) continue; // no active config — can't map channels

            var inputs = FuelChannelExtractor.Extract(channelValues, sessionMap);
            if (inputs.IsEmpty)
            {
                // No fuel-relevant channels in this message, but ECU reset classifier may still
                // have a deadline to commit. Need the state anyway.
            }

            await ProcessCarAsync(carKey, teamId, carNumber, activeRaceId.Value, channelValues, inputs, ct);
        }
    }

    private async Task ProcessCarAsync(
        string carKey, int teamId, string carNumber, int raceId,
        ChannelValue[] channelValues, FuelInputs inputs,
        CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var existingState = await stateRepository.GetAsync(carKey, ct);

        // Pick a representative timestamp for SessionStart synthesis when needed.
        var firstMessageTimestamp = EarliestTimestamp(channelValues) ?? timeProvider.GetUtcNow().UtcDateTime;

        var state = await sessionLifecycle.EnsureInitializedAsync(
            db, teamId, carNumber, raceId, existingState, inputs, firstMessageTimestamp, ct);

        state = await stintLifecycle.ApplyAsync(db, teamId, carNumber, raceId, state, inputs, ct);

        // After InPit edges are processed, fold any new LastLapTime into the rolling
        // window — StintLifecycle has already flagged in/out laps via state.
        state = paceTracker.Apply(state, inputs);

        // Update ThrottleProxy scalars + extend the cloud-side ∫ ThrottleProxyRate dt
        // BEFORE FuelWindowLifecycle so window-open baselines see the latest values.
        state = throttleProxyTracker.Apply(state, inputs);

        var anchors = refuelDetector.Detect(state, inputs);
        // Apply in chronological order so that two simultaneous anchors produce one event
        // with both anchors set (first creates, second merges).
        foreach (var anchor in anchors.OrderBy(a => a.AtUtc))
        {
            state = await fuelWindowLifecycle.RecordAnchorAsync(db, teamId, carNumber, raceId, state, anchor, ct);
        }

        // Manual entries arrive on the same stream per ADR-0005; process after anchor
        // detection so a same-message FuelFull anchor opens its window first and the
        // matching ManualFuelAddedGallons attaches volume to it (rather than creating a
        // duplicate manual event).
        state = await manualEntryHandler.ApplyAsync(db, teamId, carNumber, raceId, state, inputs, ct);

        state = await ecuResetClassifier.ProcessAsync(
            db, state, inputs, timeProvider.GetUtcNow().UtcDateTime, ct);

        state = await snapshotEmitter.MaybeEmitAsync(carKey, teamId, carNumber, raceId, state, ct);

        await stateRepository.SetAsync(carKey, state, ct);
    }

    private static DateTime? EarliestTimestamp(ChannelValue[] values)
    {
        if (values.Length == 0) return null;
        var min = DateTime.MaxValue;
        var any = false;
        for (var i = 0; i < values.Length; i++)
        {
            var ts = values[i].Timestamp;
            if (ts == default) continue;
            if (ts < min) min = ts;
            any = true;
        }
        return any ? min : null;
    }

    private async Task EnsureConsumerGroupAsync()
    {
        var db = redis.GetDatabase();
        try
        {
            await db.StreamCreateConsumerGroupAsync(
                Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
                Consts.CHANNEL_PROC_FUEL_CONSUMER_GROUP,
                StreamPosition.NewMessages,
                createStream: true);
            logger.LogInformation("Created consumer group '{Group}' on stream '{Stream}'",
                Consts.CHANNEL_PROC_FUEL_CONSUMER_GROUP, Consts.CAR_CHANNEL_VALUES_STREAM_KEY);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP"))
        {
            logger.LogDebug("Consumer group '{Group}' already exists", Consts.CHANNEL_PROC_FUEL_CONSUMER_GROUP);
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
