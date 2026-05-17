using ChannelProcessor.Telemetry;
using Cloud.Shared.Extensions;
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

        builder.Services.AddSingleton<ICarChannelStateRepository, CarChannelStateRepository>();
        builder.Services.AddHostedService<TelemetryStreamConsumer>();

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
