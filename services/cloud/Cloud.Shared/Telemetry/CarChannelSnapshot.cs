using MessagePack;

namespace Cloud.Shared.Telemetry;

/// <summary>
/// All channel snapshots for one car at a point in time, sent over SignalR
/// in full-snapshot messages.
/// </summary>
[MessagePackObject]
public class CarChannelSnapshot
{
    [Key(0)]
    public string CarKey { get; set; } = string.Empty;

    [Key(1)]
    public string CarNumber { get; set; } = string.Empty;

    [Key(2)]
    public Dictionary<ushort, ChannelValueSnapshot> Channels { get; set; } = [];

    /// <summary>
    /// The CarConfiguration ID the car is currently transmitting against, or null
    /// when no active car connection exists for this carKey. Clients use this to
    /// fetch the configuration that maps SessionIndex -> ChannelDefinition.
    /// </summary>
    [Key(3)]
    public Guid? ConfigurationId { get; set; }
}
