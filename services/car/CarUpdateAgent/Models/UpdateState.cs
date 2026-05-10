namespace CarUpdateAgent.Models;

/// <summary>
/// Snapshot of the update agent's current runtime status.
/// Returned by GET /status and maintained by the update orchestrator.
/// </summary>
public class UpdateState
{
    /// <summary>Current phase of the update lifecycle.</summary>
    public UpdateStatus Status { get; set; } = UpdateStatus.Idle;

    /// <summary>Version being applied, or the version last applied.</summary>
    public string? Version { get; set; }

    /// <summary>Human-readable error detail when Status is Failed or RolledBack.</summary>
    public string? ErrorDetail { get; set; }

    /// <summary>UTC timestamp of the last status transition.</summary>
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
