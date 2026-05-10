namespace CarUpdateAgent.Services;

public interface ISystemdService
{
    /// <summary>Stops the unit and waits for it to reach an inactive state.</summary>
    Task StopAsync(string unit, CancellationToken cancellationToken);

    /// <summary>Starts the unit.</summary>
    Task StartAsync(string unit, CancellationToken cancellationToken);

    /// <summary>Restarts the unit.</summary>
    Task RestartAsync(string unit, CancellationToken cancellationToken);

    /// <summary>
    /// Returns <c>true</c> if the unit's active state is <c>active</c>.
    /// </summary>
    Task<bool> IsActiveAsync(string unit, CancellationToken cancellationToken);

    /// <summary>
    /// Returns <c>true</c> if the unit's active state is <c>failed</c>.
    /// </summary>
    Task<bool> IsFailedAsync(string unit, CancellationToken cancellationToken);
}
