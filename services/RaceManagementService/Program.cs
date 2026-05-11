using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Web;
using RaceManagementService.Data;
using RaceManagementService.Discovery;
using RaceManagementService.Routing;

namespace RaceManagementService;

public class Program
{
    public static void Main(string[] args)
    {
        var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
        try
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Logging.ClearProviders();
            builder.Host.UseNLog();

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
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
            });

            builder.Services.AddDbContext<RaceManagementDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("RaceManagement")));

            builder.Services.AddSingleton<RacecarRegistry>();
            builder.Services.AddHostedService<RacecarDiscoveryService>();

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<RaceManagementDbContext>().Database.Migrate();
            }

            LogAssemblyInfo<Program>(app);

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                Console.Title = "Race Management";
            }

            app.UseCors("LocalUi");
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Stopped program because of exception");
            throw;
        }
        finally
        {
            LogManager.Shutdown();
        }
    }

    public static void LogAssemblyInfo<T>(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<T>>();
        var assembly = typeof(T).Assembly;

        var name = assembly.GetName().Name ?? "unknown";
        var version = assembly.GetName().Version?.ToString() ?? "unknown";

        logger.LogInformation("Service starting...");
        logger.LogInformation("Assembly: {AssemblyName}, Version: {Version}", name, version);
    }
}
