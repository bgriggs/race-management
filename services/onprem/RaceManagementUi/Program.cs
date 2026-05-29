using NLog;
using NLog.Web;

namespace RaceManagementUi;

/// <summary>
/// Hosts the race-management-local Angular SPA as a Windows service and reverse-proxies
/// API calls (/v1.0/*) to the local RaceManagementService. Serving the UI and the API
/// from the same origin avoids browser CORS entirely.
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();
        try
        {
            var builder = WebApplication.CreateBuilder(args);

            // Run under the Windows Service Control Manager; no-op when launched interactively.
            builder.Host.UseWindowsService(options => options.ServiceName = "Redmist Race Management UI");

            builder.Logging.ClearProviders();
            builder.Host.UseNLog();

            // Reverse proxy config (routes/clusters) is read from the "ReverseProxy" section
            // of appsettings.json so the backend address can be changed without rebuilding.
            builder.Services.AddReverseProxy()
                .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

            var app = builder.Build();

            // Static SPA assets (Angular build output copied into wwwroot at deploy time).
            app.UseDefaultFiles();
            app.UseStaticFiles();

            // /v1.0/** is matched by the proxy endpoints; everything else falls through to the SPA.
            app.MapReverseProxy();

            // Client-side routing: any unmatched, non-file path returns index.html.
            app.MapFallbackToFile("index.html");

            logger.Info("Race Management UI host starting...");
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
}
