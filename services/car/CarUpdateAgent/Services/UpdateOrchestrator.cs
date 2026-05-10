using CarUpdateAgent.Configuration;
using CarUpdateAgent.Models;
using Microsoft.Extensions.Options;

namespace CarUpdateAgent.Services;

public class UpdateOrchestrator : IUpdateOrchestrator
{
    private readonly IBinaryStore _binaryStore;
    private readonly IHashVerifier _hashVerifier;
    private readonly ISystemdService _systemdService;
    private readonly IUpdateDownloader _downloader;
    private readonly IRollbackWatchdog _watchdog;
    private readonly UpdateAgentOptions _options;
    private readonly ILogger<UpdateOrchestrator> _logger;

    // Prevents concurrent updates; also used by explicit Rollback.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly object _stateLock = new();
    private UpdateState _state = new();

    public UpdateOrchestrator(
        IBinaryStore binaryStore,
        IHashVerifier hashVerifier,
        ISystemdService systemdService,
        IUpdateDownloader downloader,
        IRollbackWatchdog watchdog,
        IOptions<UpdateAgentOptions> options,
        ILogger<UpdateOrchestrator> logger)
    {
        _binaryStore = binaryStore;
        _hashVerifier = hashVerifier;
        _systemdService = systemdService;
        _downloader = downloader;
        _watchdog = watchdog;
        _options = options.Value;
        _logger = logger;
    }

    public UpdateState CurrentState
    {
        get { lock (_stateLock) return _state; }
    }

    // -------------------------------------------------------------------------
    // Public entry points
    // -------------------------------------------------------------------------

    public async Task StartOtaUpdateAsync(UpdateInfo info, CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("An update is already in progress.");

        // Full pipeline runs in the background; gate released when done.
        _ = Task.Run(async () =>
        {
            try
            {
                await RunOtaPipelineAsync(info, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OTA update pipeline failed for version {Version}.", info.Version);
                SetState(UpdateStatus.Failed, info.Version, ex.Message);
            }
            finally
            {
                _gate.Release();
            }
        });
    }

    public async Task StartLaptopUpdateAsync(
        Stream binaryStream,
        string expectedSha256,
        string version,
        CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("An update is already in progress.");

        try
        {
            // Save the stream while the HTTP request is still open (stream is request-bound).
            SetState(UpdateStatus.Verifying, version);
            await _binaryStore.SaveIncomingAsync(binaryStream, cancellationToken);
        }
        catch
        {
            _gate.Release();
            throw;
        }

        // Verify + apply runs in the background; gate released when done.
        _ = Task.Run(async () =>
        {
            try
            {
                await ApplyIncomingAsync(version, expectedSha256, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Laptop update pipeline failed for version {Version}.", version);
                SetState(UpdateStatus.Failed, version, ex.Message);
            }
            finally
            {
                _gate.Release();
            }
        });
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("An update is already in progress. Cannot roll back now.");

        var version = CurrentState.Version;
        try
        {
            _binaryStore.Rollback();
            await _systemdService.RestartAsync(_options.CoreAppSystemdUnit, cancellationToken);
            SetState(UpdateStatus.RolledBack, version, "Manual rollback performed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    // -------------------------------------------------------------------------
    // Pipeline steps
    // -------------------------------------------------------------------------

    private async Task RunOtaPipelineAsync(UpdateInfo info, CancellationToken cancellationToken)
    {
        SetState(UpdateStatus.Downloading, info.Version);

        // Open the download stream and pipe it directly into the binary store.
        // The binary store is responsible for writing to disk; no direct file I/O here.
        await using var downloadStream = await _downloader.OpenAsync(info.DownloadUrl!, cancellationToken);
        await _binaryStore.SaveIncomingAsync(downloadStream, cancellationToken);

        await ApplyIncomingAsync(info.Version, info.ExpectedSha256, cancellationToken);
    }

    /// <summary>
    /// Verifies the staged binary then stops, swaps, restarts, and watches health.
    /// Expects the incoming binary to already be at <see cref="IBinaryStore.IncomingPath"/>.
    /// </summary>
    private async Task ApplyIncomingAsync(
        string version,
        string expectedSha256,
        CancellationToken cancellationToken)
    {
        // --- Verify ---
        SetState(UpdateStatus.Verifying, version);
        var valid = await _hashVerifier.VerifyAsync(_binaryStore.IncomingPath, expectedSha256, cancellationToken);
        if (!valid)
        {
            try { File.Delete(_binaryStore.IncomingPath); } catch { /* best effort */ }
            SetState(UpdateStatus.Failed, version, "SHA-256 hash mismatch. Binary was discarded.");
            return;
        }

        // --- Apply: stop → swap → start ---
        SetState(UpdateStatus.Applying, version);
        await _systemdService.StopAsync(_options.CoreAppSystemdUnit, cancellationToken);
        _binaryStore.Swap();
        await _systemdService.StartAsync(_options.CoreAppSystemdUnit, cancellationToken);

        // --- Watch health ---
        SetState(UpdateStatus.WatchingHealth, version);
        var healthy = await _watchdog.WatchAsync(cancellationToken);

        SetState(
            healthy ? UpdateStatus.Succeeded : UpdateStatus.RolledBack,
            version,
            healthy ? null : "Core app failed health checks after update. Previous binary restored.");
    }

    // -------------------------------------------------------------------------
    // State management
    // -------------------------------------------------------------------------

    private void SetState(UpdateStatus status, string? version, string? errorDetail = null)
    {
        lock (_stateLock)
        {
            _state = new UpdateState
            {
                Status = status,
                Version = version,
                ErrorDetail = errorDetail,
                LastUpdated = DateTimeOffset.UtcNow,
            };
        }

        _logger.LogInformation(
            "Update state → {Status} (version={Version}){Error}",
            status,
            version,
            errorDetail is not null ? $": {errorDetail}" : string.Empty);
    }
}
