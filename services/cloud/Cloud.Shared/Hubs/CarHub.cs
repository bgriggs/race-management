using Channels;
using Cloud.Shared.Database;
using Cloud.Shared.Models;
using Cloud.Shared.Telemetry;
using MessagePack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Diagnostics.Metrics;

namespace Cloud.Shared.Hubs;

[Authorize]
public class CarHub(IConnectionMultiplexer cacheMux, ILogger<CarHub> logger, IDbContextFactory<RaceManagementContext> db, HybridCache hcache) : Hub
{
    private static readonly Meter _meter = new("Cloud.CarHub", "1.0");
    private static int _connectionCount = 0;
    public static Gauge<int> CarConnectionsCount { get; } = _meter.CreateGauge<int>(
        "car_hub.connections.active",
        unit: "{connections}",
        description: "Number of currently active car hub connections");

    public async override Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        var clientId = Helpers.GetClientId(this);
        if (clientId == null)
        {
            logger.LogWarning("OnConnectedAsync: invalid client ID, ignoring message");
            return;
        }

        var connEntry = new CarHubConnectionState { ClientId = clientId, ConnectionId = Context.ConnectionId, ConnectedTimestamp = DateTime.UtcNow, CarKey = string.Empty };
        var cache = cacheMux.GetDatabase();
        var hashKey = new RedisKey(Consts.CAR_CONNECTIONS);
        var fieldKey = string.Format(Consts.CAR_CONNECTION, Context.ConnectionId);
        await cache.HashSetAsync(hashKey, fieldKey, MessagePackSerializer.Serialize(connEntry));
        CarConnectionsCount.Record(Interlocked.Increment(ref _connectionCount));
        logger.LogInformation("Client {id} connected: {ConnectionId}", clientId, Context.ConnectionId);
    }

    public async override Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
        var clientId = Helpers.GetClientId(this);
        var cache = cacheMux.GetDatabase();
        var hashKey = new RedisKey(Consts.CAR_CONNECTIONS);
        var fieldKey = string.Format(Consts.CAR_CONNECTION, Context.ConnectionId);
        var connBytes = await cache.HashGetAsync(hashKey, fieldKey);
        if (connBytes.HasValue)
        {
            var connState = MessagePackSerializer.Deserialize<CarHubConnectionState>(connBytes!);
            if (!string.IsNullOrEmpty(connState.CarKey))
            {
                var connByCarKey = string.Format(Consts.CAR_CONNECTION_BY_CAR, connState.CarKey);
                // Only tear down if THIS connection is still the active one for the car.
                // Guards against a reconnect race where a new connection has already
                // re-registered this car before the old connection's disconnect fires.
                var current = await cache.StringGetAsync(connByCarKey);
                if (current.HasValue && current.ToString() == Context.ConnectionId)
                {
                    await cache.KeyDeleteAsync(connByCarKey);
                    var activeConfigKey = string.Format(Consts.CAR_ACTIVE_CONFIG_KEY, connState.CarKey);
                    await cache.KeyDeleteAsync(activeConfigKey);
                    if (connState.TeamId > 0)
                    {
                        var teamSetKey = string.Format(Consts.TEAM_CONNECTED_CARS, connState.TeamId);
                        await cache.SetRemoveAsync(teamSetKey, connState.CarKey);
                        await PublishConnectionChangeAsync(cache, connState.TeamId, connState.CarKey, isConnected: false);
                    }
                }
            }
        }
        await cache.HashDeleteAsync(hashKey, fieldKey);
        CarConnectionsCount.Record(Interlocked.Decrement(ref _connectionCount));
        logger.LogInformation("Client {id} disconnected: {ConnectionId}", clientId, Context.ConnectionId);
    }

    /// <summary>
    /// Announces this car's identity to the hub so it is marked online immediately on
    /// connect, independent of whether any channel values are flowing yet. The car client
    /// invokes this on every transition into the Connected state (initial connect and
    /// after a reconnect). Idempotent for the lifetime of a single connection.
    /// </summary>
    public async Task RegisterCarAsync(string car, Guid configurationId)
    {
        var clientId = Helpers.GetClientId(this);
        if (clientId == null) return;
        var teamId = await GetTeamIdAsync(clientId);
        if (teamId <= 0) return;
        var cache = cacheMux.GetDatabase();
        var carKey = string.Format(Consts.CAR_STREAM_FIELD, teamId, car);
        await EnsureCarRegisteredAsync(cache, clientId, teamId, carKey, configurationId);
    }

    public async Task SendChannelValuesAsync(ChannelValue[] channelValues, string car, Guid configurationId)
    {
        var clientId = Helpers.GetClientId(this);
        if (clientId == null) return;
        var teamId = await GetTeamIdAsync(clientId);
        if (teamId <= 0) return;
        var cache = cacheMux.GetDatabase();
        var carKey = string.Format(Consts.CAR_STREAM_FIELD, teamId, car);
        await cache.StreamAddAsync(Consts.CAR_CHANNEL_VALUES_STREAM_KEY, carKey, MessagePackSerializer.Serialize(channelValues));

        var activeConfigKey = string.Format(Consts.CAR_ACTIVE_CONFIG_KEY, carKey);
        await cache.StringSetAsync(activeConfigKey, configurationId.ToString());

        await EnsureCarRegisteredAsync(cache, clientId, teamId, carKey, configurationId);
    }

    /// <summary>
    /// Registers the car's online state in Redis (connection-by-car pointer, connection
    /// state hash, team connected-cars set) and publishes a connect notification. Runs
    /// once per connection — guarded by <see cref="HubCallerContext.Items"/>.
    /// </summary>
    private async Task EnsureCarRegisteredAsync(IDatabase cache, string clientId, int teamId, string carKey, Guid configurationId)
    {
        if (Context.Items.ContainsKey(carKey)) return;

        var activeConfigKey = string.Format(Consts.CAR_ACTIVE_CONFIG_KEY, carKey);
        await cache.StringSetAsync(activeConfigKey, configurationId.ToString());

        var connByCarKey = string.Format(Consts.CAR_CONNECTION_BY_CAR, carKey);
        await cache.StringSetAsync(connByCarKey, Context.ConnectionId);

        // Update stored connection state with the carKey so disconnect can clean it up.
        var hashKey = new RedisKey(Consts.CAR_CONNECTIONS);
        var fieldKey = string.Format(Consts.CAR_CONNECTION, Context.ConnectionId);
        var connEntry = new CarHubConnectionState { ClientId = clientId, ConnectionId = Context.ConnectionId, ConnectedTimestamp = DateTime.UtcNow, CarKey = carKey, TeamId = teamId };
        await cache.HashSetAsync(hashKey, fieldKey, MessagePackSerializer.Serialize(connEntry));

        var teamSetKey = string.Format(Consts.TEAM_CONNECTED_CARS, teamId);
        await cache.SetAddAsync(teamSetKey, carKey);

        await PublishConnectionChangeAsync(cache, teamId, carKey, isConnected: true);

        Context.Items[carKey] = true;
    }

    private static async Task PublishConnectionChangeAsync(IDatabase cache, int teamId, string carKey, bool isConnected)
    {
        var notification = new CarConnectionChangeNotification
        {
            CarKey = carKey,
            IsConnected = isConnected,
            Timestamp = DateTime.UtcNow,
        };
        var channel = new RedisChannel(string.Format(Consts.CAR_CONNECTION_CHANGES_CHANNEL, teamId), RedisChannel.PatternMode.Literal);
        await cache.Multiplexer.GetSubscriber().PublishAsync(channel, MessagePackSerializer.Serialize(notification));
    }

    private Task<int> GetTeamIdAsync(string clientId)
    {
        return hcache.GetOrCreateAsync(
            $"team-id:{clientId}",
            async cancel =>
            {
                await using var context = await db.CreateDbContextAsync(cancel);
                var team = await context.Teams.FirstOrDefaultAsync(t => t.ClientId == clientId, cancel);
                return team?.Id ?? 0;
            }).AsTask();
    }
}
