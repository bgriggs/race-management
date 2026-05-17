using Cloud.Shared;
using Cloud.Shared.Extensions;
using Cloud.Shared.Hubs;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Caching.Hybrid;
using NLog.Extensions.Logging;
using StackExchange.Redis;

namespace CarGateway;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddNLog("NLog");

        // Add services to the container.
        builder.Services.AddKeycloakAuthentication(builder.Configuration);

        // Extract JWT from query string for SignalR WebSocket connections
        // (WebSocket API does not support custom HTTP headers, so the token is passed via ?access_token=)
        builder.Services.AddSignalRJwtFromQueryString("/car-status");

        // Configure Redis with settings for SignalR backplane in multi-replica environment
        builder.Services.AddRedisConnectionMultiplexer(builder.Configuration);

        builder.Services.AddPostgres(builder.Configuration);
        builder.Services.AddHybridCache(o => o.DefaultEntryOptions = new HybridCacheEntryOptions { Expiration = TimeSpan.FromHours(24), LocalCacheExpiration = TimeSpan.FromHours(8) });

        var redisConn = builder.Configuration["ConnectionStrings:Redis"] ?? throw new InvalidOperationException("Redis connection string is not configured");
        builder.Services.AddSignalR(o =>
        {
            o.MaximumParallelInvocationsPerClient = 3;
            o.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
            o.KeepAliveInterval = TimeSpan.FromSeconds(15);
            o.HandshakeTimeout = TimeSpan.FromSeconds(30);
        })
        .AddStackExchangeRedis(redisConn, options =>
        {
            options.Configuration.ChannelPrefix = RedisChannel.Literal("car-status");

            // Enhanced Redis configuration for reliable backplane operation
            options.Configuration.ConnectRetry = 10;
            options.Configuration.ConnectTimeout = 10000;
            options.Configuration.SyncTimeout = 10000;
            options.Configuration.AsyncTimeout = 10000;
            options.Configuration.AbortOnConnectFail = false;
            options.Configuration.KeepAlive = 60;
            options.Configuration.ReconnectRetryPolicy = new ExponentialRetry(5000);
        })
        .AddMessagePackProtocol(options =>
        {
            options.SerializerOptions = MessagePackSerializerOptions.Standard
                .WithResolver(ContractlessStandardResolver.Instance);
        });

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();
        app.LogAssemblyInfo<Program>();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            Console.Title = "CarGateway";
        }

        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthCheckEndpoints();
        app.MapHub<CarHub>("/car-status");
        app.Run();
    }
}
