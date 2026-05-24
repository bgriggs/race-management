using Channels;
using Cloud.Shared;
using Cloud.Shared.Hubs;
using Cloud.Shared.Streaming;
using MessagePack;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace CarGateway.Forwarding;

/// <summary>
/// Consumes the <c>car-channel-values</c> Redis Stream, filters values whose
/// <see cref="ChannelDefinition.Distribution"/> is <see cref="ChannelDistribution.CloudToCar"/>,
/// and forwards them to the corresponding connected car via the <see cref="CarHub"/>
/// using the <c>ReceiveChannelValues</c> client method.
/// </summary>
public class CarGatewayForwardingService(
    IConnectionMultiplexer redis,
    IHubContext<CarHub> hub,
    ICarChannelDefinitionResolver resolver,
    ILogger<CarGatewayForwardingService> logger) : BackgroundService
{
    private static readonly string _consumerName = Environment.MachineName;
    private const int BatchSize = 50;
    private const int IdlePollMs = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync();

        logger.LogInformation("CarGatewayForwardingService started (consumer: {Consumer}, group: {Group})",
            _consumerName, Consts.CARGW_CONSUMER_GROUP);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var db = redis.GetDatabase();
                var entries = await db.StreamReadGroupAsync(
                    Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
                    Consts.CARGW_CONSUMER_GROUP,
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
                        Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
                        Consts.CARGW_CONSUMER_GROUP,
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

        logger.LogInformation("CarGatewayForwardingService stopped");
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
                logger.LogWarning(ex, "Failed to deserialize ChannelValue[] for car {CarKey}, entry {EntryId}", carKey, entry.Id);
                continue;
            }

            if (channelValues.Length == 0) continue;

            var sessionIndexMap = await resolver.GetSessionIndexMapAsync(carKey, ct);
            if (sessionIndexMap is null)
            {
                // No active configuration for this car — nothing routable. Skip.
                continue;
            }

            var forwardable = new List<ChannelValue>(channelValues.Length);
            foreach (var cv in channelValues)
            {
                if (sessionIndexMap.TryGetValue(cv.SessionIndex, out var def)
                    && def.Distribution == ChannelDistribution.CloudToCar)
                {
                    forwardable.Add(cv);
                }
            }

            if (forwardable.Count == 0) continue;

            var connIdRaw = await redis.GetDatabase().StringGetAsync(
                string.Format(Consts.CAR_CONNECTION_BY_CAR, carKey));
            if (!connIdRaw.HasValue)
            {
                logger.LogDebug("Car {CarKey} has CloudToCar values to forward but no active connection; dropping",
                    carKey);
                continue;
            }

            var connectionId = connIdRaw.ToString();
            try
            {
                await hub.Clients.Client(connectionId)
                    .SendAsync("ReceiveChannelValues", forwardable.ToArray(), ct);
                logger.LogDebug("Forwarded {Count} CloudToCar values to car {CarKey}", forwardable.Count, carKey);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to forward CloudToCar values to car {CarKey} (connection {ConnId})",
                    carKey, connectionId);
            }
        }
    }

    private async Task EnsureConsumerGroupAsync()
    {
        var db = redis.GetDatabase();
        try
        {
            await db.StreamCreateConsumerGroupAsync(
                Consts.CAR_CHANNEL_VALUES_STREAM_KEY,
                Consts.CARGW_CONSUMER_GROUP,
                StreamPosition.NewMessages,
                createStream: true);
            logger.LogInformation("Created consumer group '{Group}' on stream '{Stream}'",
                Consts.CARGW_CONSUMER_GROUP, Consts.CAR_CHANNEL_VALUES_STREAM_KEY);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP"))
        {
            logger.LogDebug("Consumer group '{Group}' already exists", Consts.CARGW_CONSUMER_GROUP);
        }
    }
}
