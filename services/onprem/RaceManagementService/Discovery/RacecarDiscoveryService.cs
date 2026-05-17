using Zeroconf;

namespace RaceManagementService.Discovery;

/// <summary>
/// Background service that periodically scans the local network for racecars advertising
/// themselves via DNS-SD (<c>_racecar._tcp.local.</c>) and updates the <see cref="RacecarRegistry"/>.
///
/// Auto-selection: when the discovered list transitions from empty to non-empty and there is
/// no active car selected, the service waits 1 second to allow additional cars to appear.
/// If exactly one car is present after the wait, it becomes the active car automatically.
/// If multiple cars appear during the window, the user must choose.
///
/// Eviction: if the active car disappears from a scan result it is cleared automatically
/// (handled inside <see cref="RacecarRegistry.Update"/>).
/// </summary>
public sealed class RacecarDiscoveryService : BackgroundService
{
    private const string ServiceType = "_racecar._tcp.local.";
    private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan AutoSelectDelay = TimeSpan.FromSeconds(1);

    private readonly RacecarRegistry _registry;
    private readonly ILogger<RacecarDiscoveryService> _logger;

    // Tracks whether we are already in the auto-select delay window to avoid re-entering it.
    private bool _autoSelectPending;
    // Names visible in the previous scan — used to compute per-car appeared/disappeared diffs.
    private HashSet<string> _previousNames = [];

    public RacecarDiscoveryService(RacecarRegistry registry, ILogger<RacecarDiscoveryService> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Racecar DNS-SD discovery started (service type: {ServiceType}).", ServiceType);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ScanAsync(stoppingToken);

            try
            {
                await Task.Delay(ScanInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Racecar DNS-SD discovery stopped.");
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        try
        {
            var hosts = await ZeroconfResolver.ResolveAsync(
                ServiceType,
                scanTime: ScanTimeout,
                cancellationToken: ct).ConfigureAwait(false);

            var discovered = hosts
                .SelectMany(h => h.Services.Values.Select(svc => new DiscoveredRacecar(
                    Name: h.DisplayName,
                    Host: h.IPAddress,
                    Port: svc.Port)))
                .ToList();

            var currentNames = discovered.Select(c => c.Name).ToHashSet();
            var (wasEmpty, _, evictedActiveName) = _registry.Update(discovered);

            // Log individual appearance / disappearance diffs.
            foreach (var car in discovered.Where(c => !_previousNames.Contains(c.Name)))
                _logger.LogDebug("Racecar '{Name}' discovered at {BaseUrl}.", car.Name, car.BaseUrl);

            foreach (var goneName in _previousNames.Where(n => !currentNames.Contains(n)))
                _logger.LogDebug("Racecar '{Name}' is no longer visible.", goneName);

            if (evictedActiveName is not null)
                _logger.LogDebug(
                    "Active racecar '{Name}' is no longer visible; deselecting.", evictedActiveName);

            _previousNames = currentNames;

            if (discovered.Count > 0)
            {
                // Trigger auto-selection window when transitioning from no cars to cars present
                // and there is no active car yet.
                if (wasEmpty && _registry.ActiveRacecar is null && !_autoSelectPending)
                {
                    _autoSelectPending = true;
                    _ = RunAutoSelectAsync(ct);
                }
            }
            else
            {
                _autoSelectPending = false;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down — swallow.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DNS-SD scan failed; will retry in {Interval}.", ScanInterval);
        }
    }

    /// <summary>
    /// Waits <see cref="AutoSelectDelay"/> then calls <see cref="RacecarRegistry.TryAutoSelect"/>
    /// so the user has a chance to pick manually if multiple cars appeared during the window.
    /// </summary>
    private async Task RunAutoSelectAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(AutoSelectDelay, ct).ConfigureAwait(false);
            _registry.TryAutoSelect();

            if (_registry.ActiveRacecar is { } active)
                _logger.LogInformation(
                    "Auto-selected racecar '{Name}' at {BaseUrl}.", active.Name, active.BaseUrl);
            else
                _logger.LogInformation(
                    "Multiple racecars discovered; waiting for user to select an active car.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down — fine.
        }
        finally
        {
            _autoSelectPending = false;
        }
    }
}
