namespace Channels;

/// <summary>
/// What entity a channel's values are bound to. PerCar values are keyed by
/// (TeamId, CarId, ChannelId); PerTeam values are keyed by (TeamId, ChannelId)
/// and shared across every car on the team (e.g., a race-wide flag state).
/// </summary>
public enum ChannelScope
{
    /// <summary>Value belongs to a specific car (default).</summary>
    PerCar = 0,

    /// <summary>Value is shared across all cars on a team.</summary>
    PerTeam = 1,
}
