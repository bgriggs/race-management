using Cloud.Shared.Extensions;
using NLog.Extensions.Logging;

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

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            Console.Title = "CarGateway";
        }

        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthCheckEndpoints();
        app.Run();
    }
}
