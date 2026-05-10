
using CarUpdateAgent.BackgroundServices;
using CarUpdateAgent.Configuration;
using CarUpdateAgent.Services;

namespace CarUpdateAgent;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ---------------------------------------------------------------
        // Configuration
        // ---------------------------------------------------------------
        builder.Services.Configure<UpdateAgentOptions>(
            builder.Configuration.GetSection(UpdateAgentOptions.SectionName));

        // ---------------------------------------------------------------
        // Core services
        // ---------------------------------------------------------------
        builder.Services.AddSingleton<IBinaryStore, BinaryStore>();
        builder.Services.AddSingleton<IHashVerifier, HashVerifier>();
        builder.Services.AddSingleton<ISystemdService, SystemdService>();

        // Named HttpClient for binary downloads (no base address — URLs are signed and absolute).
        builder.Services.AddHttpClient<IUpdateDownloader, UpdateDownloader>("Downloader");

        // Named HttpClient for the rollback watchdog health checks.
        builder.Services.AddHttpClient<IRollbackWatchdog, RollbackWatchdog>("HealthCheck", (sp, client) =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpdateAgentOptions>>().Value;
            client.BaseAddress = new Uri(opts.CoreAppHealthBaseUrl);
        });

        // Orchestrator is singleton so state is shared across the controller and the cloud hub client.
        builder.Services.AddSingleton<IUpdateOrchestrator, UpdateOrchestrator>();

        // ---------------------------------------------------------------
        // Background services
        // ---------------------------------------------------------------
        builder.Services.AddHostedService<CloudHubClient>();

        // ---------------------------------------------------------------
        // Web API
        // ---------------------------------------------------------------
        builder.Services.AddControllers();

        // Disable request body buffering so the binary stream is read directly
        // from the socket into the staging file without copying to memory.
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.Limits.MaxRequestBodySize = 500 * 1024 * 1024; // 500 MB
        });

        var app = builder.Build();

        app.MapControllers();

        app.Run();
    }
}
