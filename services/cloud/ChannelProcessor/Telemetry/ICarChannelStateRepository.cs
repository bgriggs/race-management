using Cloud.Shared.Telemetry;

namespace ChannelProcessor.Telemetry;

/// <summary>
/// Stores and retrieves the latest channel value state per car in Redis,
/// with change detection and pub/sub change notification.
/// </summary>
public interface ICarChannelStateRepository
{
    /// <summary>
    /// Compares <paramref name="incoming"/> against the stored snapshot for this channel.
    /// If the value has changed (or no snapshot exists), persists the new snapshot, publishes
    /// a change notification, and returns <c>true</c>. Returns <c>false</c> when unchanged.
    /// </summary>
    Task<bool> SetIfChangedAsync(string carKey, ushort sessionIndex, ChannelValueSnapshot incoming, CancellationToken ct = default);

    /// <summary>
    /// Returns all stored channel snapshots for the given car key, keyed by session index.
    /// Returns an empty dictionary if no state exists for the car.
    /// </summary>
    Task<Dictionary<ushort, ChannelValueSnapshot>> GetAllAsync(string carKey, CancellationToken ct = default);

    /// <summary>
    /// Removes all stored channel state for the given car key (e.g., on CarDisconnected).
    /// </summary>
    Task ClearAsync(string carKey, CancellationToken ct = default);
}
