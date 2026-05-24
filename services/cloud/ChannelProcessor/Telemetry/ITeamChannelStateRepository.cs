using Cloud.Shared.Telemetry;

namespace ChannelProcessor.Telemetry;

/// <summary>
/// Stores and retrieves the latest PerTeam channel value state per team in Redis,
/// with change detection and pub/sub change notification. Keyed by stable
/// ChannelId Guid rather than SessionIndex, because PerTeam channels are
/// addressed at the team level — SessionIndex is meaningful only per-car.
/// </summary>
public interface ITeamChannelStateRepository
{
    /// <summary>
    /// Compares <paramref name="incoming"/> against the stored snapshot for this
    /// (team, channelId) pair. If the value has changed (or no snapshot exists),
    /// persists the new snapshot, publishes a change notification, and returns
    /// <c>true</c>. Returns <c>false</c> when unchanged.
    /// </summary>
    Task<bool> SetIfChangedAsync(int teamId, Guid channelId, ChannelValueSnapshot incoming, CancellationToken ct = default);

    /// <summary>
    /// Returns all stored team channel snapshots for the given team, keyed by ChannelId.
    /// Returns an empty dictionary if no state exists for the team.
    /// </summary>
    Task<Dictionary<Guid, ChannelValueSnapshot>> GetAllAsync(int teamId, CancellationToken ct = default);
}
