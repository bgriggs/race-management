using Cloud.Shared.Auth;
using Cloud.Shared.Extensions;
using Cloud.Shared.Hubs;
using Cloud.Shared.Telemetry;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Caching.Hybrid;
using NLog.Extensions.Logging;
using WebApi.Telemetry;

namespace WebApi;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddNLog("NLog");

        // Add services to the container.
        builder.Services.AddControllers(options =>
            options.Conventions.Add(new RouteTokenTransformerConvention(new KebabCaseParameterTransformer())));
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("LocalUi", policy =>
            {
                policy.WithOrigins("http://localhost:4200")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        // Add services to the container.
        builder.Services.AddKeycloakAuthentication(builder.Configuration);

        // Extract JWT from query string for SignalR WebSocket connections
        // (WebSocket API does not support custom HTTP headers, so the token is passed via ?access_token=)
        builder.Services.AddSignalRJwtFromQueryString("/web-status");

        builder.Services.AddPostgres(builder.Configuration);

        // Configure Redis with settings for SignalR backplane in multi-replica environment
        builder.Services.AddRedisConnectionMultiplexer(builder.Configuration);

        builder.Services.AddSignalR(o =>
        {
            o.MaximumParallelInvocationsPerClient = 3;
            o.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
            o.KeepAliveInterval = TimeSpan.FromSeconds(15);
            o.HandshakeTimeout = TimeSpan.FromSeconds(30);
        })
        .AddRedisBackplane(builder.Services, channelPrefix: "web-status")
        .AddMessagePackProtocol(options =>
        {
            options.SerializerOptions = MessagePackSerializerOptions.Standard
                .WithResolver(ContractlessStandardResolver.Instance);
        });

        builder.Services.AddTeamRoleContext();
        builder.Services.AddHybridCache(o => o.DefaultEntryOptions = new HybridCacheEntryOptions { Expiration = TimeSpan.FromHours(24), LocalCacheExpiration = TimeSpan.FromHours(12) });

        builder.Services.AddSingleton<IConnectedTeamsTracker, ConnectedTeamsTracker>();
        builder.Services.AddSingleton<ITeamChannelSnapshotService, TeamChannelSnapshotService>();
        builder.Services.AddHostedService<ChannelPropagatorService>();

        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        });

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        var app = builder.Build();
        app.LogAssemblyInfo<Program>();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            Console.Title = "WebAPI";
            app.UseCors("LocalUi");
        }

        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthCheckEndpoints();
        app.MapHub<WebHub>("/web-status");
        app.Run();
    }
}
