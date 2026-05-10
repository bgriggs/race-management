namespace CarUpdateAgent.Services;

public interface IRollbackWatchdog
{
    /// <summary>
    /// Monitors the core app's health endpoints after an update.
    /// Polls <c>/health/startup</c> until it succeeds or times out, then monitors
    /// <c>/health/live</c> for a further window. Automatically performs a binary rollback
    /// and restarts the core app if any check fails.
    /// </summary>
    /// <returns><c>true</c> if the core app remained healthy; <c>false</c> if rollback was triggered.</returns>
    Task<bool> WatchAsync(CancellationToken cancellationToken);
}
