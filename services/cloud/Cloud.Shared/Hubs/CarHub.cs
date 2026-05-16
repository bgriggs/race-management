using Channels;
using Cloud.Shared.Models;
using MessagePack;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Diagnostics.Metrics;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Cloud.Shared.Hubs;

[Authorize]
public class CarHub(IConnectionMultiplexer cacheMux, ILogger<CarHub> logger) : Hub
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

        var connEntry = new CarHubConnectionState { ClientId = clientId, ConnectionId = Context.ConnectionId, ConnectedTimestamp = DateTime.UtcNow };
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
        await cache.HashDeleteAsync(hashKey, fieldKey);
        CarConnectionsCount.Record(Interlocked.Decrement(ref _connectionCount));
        logger.LogInformation("Client {id} disconnected: {ConnectionId}", clientId, Context.ConnectionId);
    }

    public async Task SendChannelValues(ChannelValue[] channelValues, string car)
    {
        var streamId = string.Format(Consts.CAR_CHANNEL_VALUES_STREAM_KEY);
        var cache = cacheMux.GetDatabase();
        await cache.StreamAddAsync(streamId, string.Format(Consts.CAR_STREAM_FIELD, eventId, car), command);
    }
}
