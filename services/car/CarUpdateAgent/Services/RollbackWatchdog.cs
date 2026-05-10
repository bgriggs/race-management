using CarUpdateAgent.Configuration;
using Microsoft.Extensions.Options;

namespace CarUpdateAgent.Services;

public class RollbackWatchdog : IRollbackWatchdog
{
    private readonly HttpClient _httpClient;
    private readonly IBinaryStore _binaryStore;
    private readonly ISystemdService _systemdService;
    private readonly UpdateAgentOptions _options;
    private readonly ILogger<RollbackWatchdog> _logger;

    public RollbackWatchdog(
        HttpClient httpClient,
        IBinaryStore binaryStore,
        ISystemdService systemdService,
        IOptions<UpdateAgentOptions> options,
        ILogger<RollbackWatchdog> logger)
    {
        _httpClient = httpClient;
        _binaryStore = binaryStore;
        _systemdService = systemdService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> WatchAsync(CancellationToken cancellationToken)
    {
        // Phase 1: wait for startup health check to succeed.
        _logger.LogInformation("Watchdog: waiting for startup health check (timeout {Seconds}s).",
            _options.WatchdogStartupTimeoutSeconds);

        var startupTimeout = TimeSpan.FromSeconds(_options.WatchdogStartupTimeoutSeconds);
        if (!await WaitForHealthAsync("/health/startup", startupTimeout, cancellationToken))
        {
            _logger.LogWarning("Watchdog: startup health check did not succeed in time. Rolling back.");
            await PerformRollbackAsync(cancellationToken);
            return false;
        }

        // Phase 2: monitor liveness for the configured window.
        _logger.LogInformation("Watchdog: startup healthy. Monitoring liveness for {Seconds}s.",
            _options.WatchdogLivenessWindowSeconds);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(_options.WatchdogLivenessWindowSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            if (!await CheckHealthAsync("/health/live", cancellationToken))
            {
                _logger.LogWarning("Watchdog: liveness check failed. Rolling back.");
                await PerformRollbackAsync(cancellationToken);
                return false;
            }
        }

        _logger.LogInformation("Watchdog: core app is healthy. Update complete.");
        return true;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Polls <paramref name="path"/> every 2 seconds until it returns a success status
    /// or <paramref name="timeout"/> elapses. Propagates outer cancellation.
    /// </summary>
    private async Task<bool> WaitForHealthAsync(
        string path,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            while (true)
            {
                if (await CheckHealthAsync(path, timeoutCts.Token))
                    return true;

                await Task.Delay(TimeSpan.FromSeconds(2), timeoutCts.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Watchdog timeout fired (not the outer application cancellation).
            return false;
        }
    }

    /// <summary>
    /// Issues a single GET to <paramref name="path"/>. Returns false on any non-success
    /// response or network error; propagates <see cref="OperationCanceledException"/>.
    /// </summary>
    private async Task<bool> CheckHealthAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(path, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug("Watchdog: {Path} check failed — {Message}", path, ex.Message);
            return false;
        }
    }

    private async Task PerformRollbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            _binaryStore.Rollback();
            await _systemdService.RestartAsync(_options.CoreAppSystemdUnit, cancellationToken);
            _logger.LogInformation("Watchdog: rollback complete.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Watchdog: rollback failed.");
            throw;
        }
    }
}
