namespace ChannelProcessor.FuelAnalysis.State;

/// <summary>
/// Per-car Fuel Reconciler runtime state in Redis. Single-writer per car (the consumer
/// group serializes message delivery per car) — no optimistic concurrency required.
/// </summary>
public interface ICarFuelStateRepository
{
    /// <summary>Returns the stored state for the car, or <c>null</c> if none exists.</summary>
    Task<CarFuelState?> GetAsync(string carKey, CancellationToken ct = default);

    /// <summary>Overwrites the stored state for the car.</summary>
    Task SetAsync(string carKey, CarFuelState state, CancellationToken ct = default);

    /// <summary>Removes the stored state for the car (e.g., on session end).</summary>
    Task ClearAsync(string carKey, CancellationToken ct = default);
}
