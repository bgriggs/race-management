namespace CarUpdateAgent.Models;

public enum UpdateStatus
{
    /// <summary>No update in progress. Ready to accept an update request.</summary>
    Idle,

    /// <summary>Downloading the new binary from cloud object storage.</summary>
    Downloading,

    /// <summary>Verifying the SHA-256 hash of the downloaded binary.</summary>
    Verifying,

    /// <summary>Stopping the core app, swapping the binary, and restarting.</summary>
    Applying,

    /// <summary>Core app restarted; monitoring health endpoints for startup and liveness.</summary>
    WatchingHealth,

    /// <summary>Update succeeded and the core app is healthy.</summary>
    Succeeded,

    /// <summary>Core app failed health checks after update; previous binary was restored.</summary>
    RolledBack,

    /// <summary>Update failed due to an unrecoverable error (e.g. hash mismatch, download failure).</summary>
    Failed,
}
