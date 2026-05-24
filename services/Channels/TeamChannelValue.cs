using MessagePack;

namespace Channels;

/// <summary>
/// Wire payload for cloud-produced PerTeam (Scope = PerTeam) channel values flowing
/// on the <c>team-channel-values</c> Redis Stream. Unlike <see cref="ChannelValue"/>,
/// this type is keyed by the channel's stable <see cref="ChannelDefinition.Id"/>
/// Guid rather than a per-car SessionIndex, because a single team-scoped value
/// fans out to every connected car in the team — each of which may have the channel
/// at a different SessionIndex in its own configuration.
/// </summary>
[MessagePackObject]
public class TeamChannelValue
{
    /// <summary>
    /// Stable channel identifier (<see cref="ChannelDefinition.Id"/>). Each receiving
    /// car resolves this to its own SessionIndex at delivery time.
    /// </summary>
    [Key(0)]
    public Guid ChannelId { get; set; }

    [Key(1)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Wall-clock UTC timestamp this value was produced.
    /// </summary>
    [Key(2)]
    public DateTime Timestamp { get; set; }
}
