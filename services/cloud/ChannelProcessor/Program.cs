using ChannelProcessor.Alarms;
using ChannelProcessor.Alarms.Config;
using ChannelProcessor.Alarms.Persistence;
using ChannelProcessor.Telemetry;
using Cloud.Shared.Alarms;
using Cloud.Shared.Extensions;
using Cloud.Shared.Streaming;
using Microsoft.Extensions.Caching.Hybrid;
using NLog.Extensions.Logging;

namespace ChannelProcessor;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddNLog("NLog");

        // Add services to the container.

        // Configure Redis with settings for SignalR backplane in multi-replica environment
        builder.Services.AddRedisConnectionMultiplexer(builder.Configuration);

        builder.Services.AddPostgres(builder.Configuration);

        builder.Services.AddSingleton(TimeProvider.System);
#pragma warning disable EXTEXP0018 // HybridCache is still tagged experimental in 10.0
        builder.Services.AddHybridCache(o => o.DefaultEntryOptions = new HybridCacheEntryOptions { Expiration = TimeSpan.FromMinutes(5), LocalCacheExpiration = TimeSpan.FromMinutes(5) });
#pragma warning restore EXTEXP0018

        builder.Services.AddSingleton<ICarChannelStateRepository, CarChannelStateRepository>();
        builder.Services.AddHostedService<TelemetryStreamConsumer>();

        builder.Services.AddSingleton<ITeamChannelStateRepository, TeamChannelStateRepository>();
        builder.Services.AddHostedService<TeamChannelStreamConsumer>();

        // Alarm Processor — see plan.md "Alarm Processor (ChannelProcessor / Alarms)".
        builder.Services.AddSingleton<ICarChannelDefinitionResolver, CarChannelDefinitionResolver>();
        builder.Services.AddSingleton<ICarChannelPublisher, CarChannelPublisher>();
        builder.Services.AddSingleton<ITeamChannelPublisher, TeamChannelPublisher>();
        builder.Services.AddSingleton<IRedisAlarmStateGateway, RedisAlarmStateGateway>();
        builder.Services.AddSingleton<IAlarmDefinitionRepository, AlarmDefinitionRepository>();
        builder.Services.AddSingleton<ActiveAlarmStore>();
        builder.Services.AddHostedService<AlarmProcessorWorker>();
        builder.Services.AddHostedService<AlarmConfigChangeListener>();

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();
        app.LogAssemblyInfo<Program>();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            Console.Title = "ChannelProcessor";
        }

        app.UseAuthorization();


        app.MapControllers();
        app.MapHealthCheckEndpoints();

        app.Run();
    }
}
