using Channels;
using Cloud.Shared.Database;
using Cloud.Shared.Models;
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
                await cache.KeyDeleteAsync(connByCarKey);
                var activeConfigKey = string.Format(Consts.CAR_ACTIVE_CONFIG_KEY, connState.CarKey);
                await cache.KeyDeleteAsync(activeConfigKey);
            }
        }
        await cache.HashDeleteAsync(hashKey, fieldKey);
        CarConnectionsCount.Record(Interlocked.Decrement(ref _connectionCount));
        logger.LogInformation("Client {id} disconnected: {ConnectionId}", clientId, Context.ConnectionId);
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

        if (!Context.Items.ContainsKey(carKey))
        {
            var connByCarKey = string.Format(Consts.CAR_CONNECTION_BY_CAR, carKey);
            await cache.StringSetAsync(connByCarKey, Context.ConnectionId);
            // Update stored connection state with the carKey so disconnect can clean it up
            var hashKey = new RedisKey(Consts.CAR_CONNECTIONS);
            var fieldKey = string.Format(Consts.CAR_CONNECTION, Context.ConnectionId);
            var connEntry = new CarHubConnectionState { ClientId = clientId, ConnectionId = Context.ConnectionId, ConnectedTimestamp = DateTime.UtcNow, CarKey = carKey };
            await cache.HashSetAsync(hashKey, fieldKey, MessagePackSerializer.Serialize(connEntry));
            Context.Items[carKey] = true;
        }
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
