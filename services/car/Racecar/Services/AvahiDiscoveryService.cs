using System.Diagnostics;

namespace Racecar.Services;

/// <summary>
/// Advertises this car's management REST API via DNS-SD (service type <c>_racecar._tcp</c>)
/// using the Avahi daemon, enabling automatic discovery by the pit laptop tool on the
/// car-local network without any manual IP or hostname configuration.
///
/// The service spawns <c>avahi-publish -s &lt;hostname&gt; _racecar._tcp &lt;port&gt;</c> and
/// keeps it alive for the lifetime of the application. If the child process exits
/// unexpectedly it is restarted after a short delay.
///
/// This service is a no-op on non-Linux platforms (e.g., development on Windows/macOS)
/// or when <c>avahi-publish</c> is not installed.
/// </summary>
public sealed class AvahiDiscoveryService : BackgroundService
{
    private const string ServiceType = "_racecar._tcp";
    private const string AvahiPublishBin = "avahi-publish";

    private readonly ILogger<AvahiDiscoveryService> _logger;
    private readonly IConfiguration _configuration;

    public AvahiDiscoveryService(ILogger<AvahiDiscoveryService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!OperatingSystem.IsLinux())
        {
            _logger.LogInformation("DNS-SD advertisement skipped: not running on Linux.");
            return;
        }

        var port = ResolvePort();
        var serviceName = Environment.MachineName;

        _logger.LogInformation(
            "Advertising DNS-SD service '{ServiceName}' as {ServiceType} on port {Port}.",
            serviceName, ServiceType, port);

        while (!stoppingToken.IsCancellationRequested)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = AvahiPublishBin,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                },
                EnableRaisingEvents = true,
            };

            // avahi-publish -s <name> <service-type> <port>
            process.StartInfo.ArgumentList.Add("-s");
            process.StartInfo.ArgumentList.Add(serviceName);
            process.StartInfo.ArgumentList.Add(ServiceType);
            process.StartInfo.ArgumentList.Add(port.ToString());

            try
            {
                process.Start();
                _logger.LogInformation("avahi-publish started (PID {Pid}).", process.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not start avahi-publish. DNS-SD advertisement unavailable.");
                return;
            }

            try
            {
                await process.WaitForExitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                try { process.Kill(); } catch { /* already exited */ }
                _logger.LogInformation("DNS-SD advertisement stopped.");
                return;
            }

            _logger.LogWarning(
                "avahi-publish exited unexpectedly (exit code {ExitCode}). Restarting in 5 seconds.",
                process.ExitCode);

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Resolves the HTTP port to advertise by inspecting the ASP.NET Core URL configuration.
    /// Falls back to 5244 if no port can be determined.
    /// </summary>
    private int ResolvePort()
    {
        // Check configuration keys set by Kestrel / ASPNETCORE_URLS / --urls.
        var urls = _configuration["urls"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
            ?? string.Empty;

        foreach (var raw in urls.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = raw.Trim()
                .Replace("0.0.0.0", "127.0.0.1", StringComparison.Ordinal)
                .Replace("[::]", "127.0.0.1", StringComparison.Ordinal)
                .Replace("*", "127.0.0.1", StringComparison.Ordinal);

            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) && uri.Port > 0)
                return uri.Port;
        }

        const int defaultPort = 5244;
        _logger.LogDebug("Could not resolve port from URL configuration; defaulting to {Port}.", defaultPort);
        return defaultPort;
    }
}
