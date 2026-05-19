using Channels;
using Cloud.Shared;
using Cloud.Shared.Telemetry;
using MessagePack;
using StackExchange.Redis;

namespace ChannelProcessor.Telemetry;

/// <summary>
/// Consumes the <c>car-channel-values</c> Redis Stream, stores the latest value for each
/// channel per car in a Redis Hash, and publishes a pub/sub change notification when a
/// value differs from the previously stored one.
/// </summary>
public class TelemetryStreamConsumer(
    IConnectionMultiplexer redis,
    ICarChannelStateRepository channelState,
    ILogger<TelemetryStreamConsumer> logger) : BackgroundService
{
    private static readonly string _consumerName = Environment.MachineName;
    private const int BatchSize = 50;
    private const int IdlePollMs = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync();

        logger.LogInformation("TelemetryStreamConsumer started (consumer: {Consumer}, group: {Group})",
            _consumerName, Consts.CHANNEL_PROC_CONSUMER_GROUP);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var db = redis.GetDatabase();
                var entries = await db.StreamReadGroupAsync(
                    Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
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

                    // ACK only after all state writes and pub/sub publishes are complete.
                    await db.StreamAcknowledgeAsync(
                        Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
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
                logger.LogError(ex, "Error reading from telemetry stream; will retry");
                await Task.Delay(1000, stoppingToken);
            }
        }

        logger.LogInformation("TelemetryStreamConsumer stopped");
    }

    private async Task ProcessEntryAsync(StreamEntry entry, CancellationToken ct)
    {
        foreach (var field in entry.Values)
        {
            var carKey = field.Name.ToString();
            if (!field.Value.HasValue) continue;

            ChannelValue[] channelValues;
            try
            {
                channelValues = MessagePackSerializer.Deserialize<ChannelValue[]>((byte[])field.Value!, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to deserialize ChannelValue[] for car {CarKey}, stream entry {EntryId}", carKey, entry.Id);
                continue;
            }

            int changedCount = 0;
            foreach (var cv in channelValues)
            {
                var snapshot = new ChannelValueSnapshot { Value = cv.Value, Timestamp = cv.Timestamp };
                var changed = await channelState.SetIfChangedAsync(carKey, cv.SessionIndex, snapshot, ct);
                if (changed) changedCount++;
            }

            if (changedCount > 0)
                logger.LogDebug("Car {CarKey}: {Changed}/{Total} channels changed", carKey, changedCount, channelValues.Length);
        }
    }

    private async Task EnsureConsumerGroupAsync()
    {
        var db = redis.GetDatabase();
        try
        {
            await db.StreamCreateConsumerGroupAsync(
                Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
                Consts.CHANNEL_PROC_CONSUMER_GROUP,
                StreamPosition.NewMessages,
                createStream: true);
            logger.LogInformation("Created consumer group '{Group}' on stream '{Stream}'",
                Consts.CHANNEL_PROC_CONSUMER_GROUP, Consts.CAR_CHANNEL_VALUES_STREAM_KEY);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP"))
        {
            logger.LogDebug("Consumer group '{Group}' already exists", Consts.CHANNEL_PROC_CONSUMER_GROUP);
        }
    }
}
