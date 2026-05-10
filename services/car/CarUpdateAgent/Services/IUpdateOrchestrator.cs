using CarUpdateAgent.Models;

namespace CarUpdateAgent.Services;

public interface IUpdateOrchestrator
{
    /// <summary>Current snapshot of the update lifecycle state.</summary>
    UpdateState CurrentState { get; }

    /// <summary>
    /// Initiates an OTA update. Returns immediately; the download, verify, apply, and
    /// health-watch pipeline runs in the background. Poll <see cref="CurrentState"/> for progress.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an update is already in progress.</exception>
    Task StartOtaUpdateAsync(UpdateInfo info, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the binary from <paramref name="binaryStream"/> (blocking until the stream is
    /// consumed), then runs verify, apply, and health-watch in the background.
    /// The caller must keep the stream open until this method returns.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an update is already in progress.</exception>
    Task StartLaptopUpdateAsync(
        Stream binaryStream,
        string expectedSha256,
        string version,
        CancellationToken cancellationToken);

    /// <summary>
    /// Manually rolls back to the previous binary and restarts the core app.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when an update is already in progress.</exception>
    Task RollbackAsync(CancellationToken cancellationToken);
}
