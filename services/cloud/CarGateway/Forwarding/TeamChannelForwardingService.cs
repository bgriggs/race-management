using Channels;
using Cloud.Shared;
using Cloud.Shared.Hubs;
using Cloud.Shared.Streaming;
using MessagePack;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace CarGateway.Forwarding;

/// <summary>
/// Consumes the <c>team-channel-values</c> Redis Stream and fans out each value to every
/// connected car in the team. For each receiving car, the stable ChannelId Guid is
/// translated to that car's per-config SessionIndex via <see cref="ICarChannelDefinitionResolver"/>
/// before being sent over <see cref="CarHub"/> as a <see cref="ChannelValue"/>.
/// </summary>
public class TeamChannelForwardingService(
    IConnectionMultiplexer redis,
    IHubContext<CarHub> hub,
    ICarChannelDefinitionResolver resolver,
    ILogger<TeamChannelForwardingService> logger) : BackgroundService
{
    private static readonly string _consumerName = Environment.MachineName;
    private const int BatchSize = 50;
    private const int IdlePollMs = 50;
    private const string TeamFieldPrefix = "team-";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnsureConsumerGroupAsync();

        logger.LogInformation("TeamChannelForwardingService started (consumer: {Consumer}, group: {Group})",
            _consumerName, Consts.CARGW_CONSUMER_GROUP);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var db = redis.GetDatabase();
                var entries = await db.StreamReadGroupAsync(
                    Consts.TEAM_CHANNEL_VALUES_STREAM_KEY,
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
                        Consts.TEAM_CHANNEL_VALUES_STREAM_KEY,
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
                logger.LogError(ex, "Error reading from team-channel-values stream; will retry");
                await Task.Delay(1000, stoppingToken);
            }
        }

        logger.LogInformation("TeamChannelForwardingService stopped");
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

            if (values.Length == 0) continue;

            await FanOutAsync(teamId, values, ct);
        }
    }

    private async Task FanOutAsync(int teamId, TeamChannelValue[] values, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var teamSetKey = string.Format(Consts.TEAM_CONNECTED_CARS, teamId);
        var carKeys = await db.SetMembersAsync(teamSetKey);
        if (carKeys.Length == 0)
        {
            logger.LogDebug("Team {TeamId} has team channel values but no connected cars; dropping", teamId);
            return;
        }

        foreach (var carKeyRaw in carKeys)
        {
            var carKey = carKeyRaw.ToString();
            var channelIdMap = await resolver.GetChannelIdMapAsync(carKey, ct);
            if (channelIdMap is null)
            {
                logger.LogDebug("Car {CarKey} (team {TeamId}) has no resolvable active configuration; skipping fan-out", carKey, teamId);
                continue;
            }

            var perCar = new List<ChannelValue>(values.Length);
            foreach (var v in values)
            {
                if (channelIdMap.TryGetValue(v.ChannelId, out var sessionIndex))
                {
                    perCar.Add(new ChannelValue { SessionIndex = sessionIndex, Value = v.Value, Timestamp = v.Timestamp });
                }
                // Channels not present in this car's configuration are silently skipped:
                // the configuration may simply not have opted into the feature that owns
                // this channel, which is legitimate.
            }

            if (perCar.Count == 0) continue;

            var connIdRaw = await db.StringGetAsync(string.Format(Consts.CAR_CONNECTION_BY_CAR, carKey));
            if (!connIdRaw.HasValue)
            {
                // Member of the team set but no active connection — likely a stale entry
                // from a missed disconnect handler. Skip; the next disconnect cycle or a
                // future reconciliation will clean it up.
                logger.LogDebug("Car {CarKey} is in team set but has no current connection; skipping", carKey);
                continue;
            }

            try
            {
                await hub.Clients.Client(connIdRaw.ToString())
                    .SendAsync("ReceiveChannelValues", perCar.ToArray(), ct);
                logger.LogDebug("Forwarded {Count} team values to car {CarKey} (team {TeamId})", perCar.Count, carKey, teamId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to forward team values to car {CarKey} (team {TeamId})", carKey, teamId);
            }
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
                Consts.CARGW_CONSUMER_GROUP,
                StreamPosition.NewMessages,
                createStream: true);
            logger.LogInformation("Created consumer group '{Group}' on stream '{Stream}'",
                Consts.CARGW_CONSUMER_GROUP, Consts.TEAM_CHANNEL_VALUES_STREAM_KEY);
        }
        catch (RedisServerException ex) when (ex.Message.StartsWith("BUSYGROUP"))
        {
            logger.LogDebug("Consumer group '{Group}' already exists on stream '{Stream}'",
                Consts.CARGW_CONSUMER_GROUP, Consts.TEAM_CHANNEL_VALUES_STREAM_KEY);
        }
    }
}
