using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Cloud.Shared.Extensions;

public static class RedisExtensions
{
    /// <summary>
    /// Wires the SignalR Redis backplane to reuse the already-registered
    /// <see cref="IConnectionMultiplexer"/> (call <see cref="AddRedisConnectionMultiplexer"/> first)
    /// rather than opening a second Redis connection.
    /// </summary>
    public static ISignalRServerBuilder AddRedisBackplane(
        this ISignalRServerBuilder signalRBuilder,
        IServiceCollection services,
        string channelPrefix)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer))
            ?? throw new InvalidOperationException(
                $"{nameof(AddRedisBackplane)} requires {nameof(IConnectionMultiplexer)} to be registered first " +
                $"(call {nameof(AddRedisConnectionMultiplexer)}).");
        var mux = (IConnectionMultiplexer)descriptor.ImplementationInstance!;

        return signalRBuilder.AddStackExchangeRedis(options =>
        {
            options.ConnectionFactory = _ => Task.FromResult(mux);
            options.Configuration.ChannelPrefix = RedisChannel.Literal(channelPrefix);
        });
    }

    /// <summary>
    /// Registers a <see cref="IConnectionMultiplexer"/> singleton configured for resilient
    /// SignalR backplane use in a multi-replica environment.
    /// Reads the connection string from <c>ConnectionStrings:Redis</c> in configuration.
    /// </summary>
    public static IServiceCollection AddRedisConnectionMultiplexer(this IServiceCollection services, IConfiguration configuration)
    {
        string redisConn = configuration.GetConnectionString("Redis") ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");

        var redisOptions = ConfigurationOptions.Parse(redisConn);
        redisOptions.AbortOnConnectFail = false;
        redisOptions.ConnectRetry = 10;
        redisOptions.ConnectTimeout = 10000; // 10 seconds
        redisOptions.SyncTimeout = 10000;
        redisOptions.AsyncTimeout = 10000;
        redisOptions.KeepAlive = 60;
        redisOptions.ReconnectRetryPolicy = new ExponentialRetry(5000);

        var redisMux = ConnectionMultiplexer.Connect(redisOptions);
        services.AddSingleton<IConnectionMultiplexer>(redisMux);
        services.AddHealthChecks().AddRedis(redisConn, tags: ["cache", "redis"]);
        return services;
    }
}
