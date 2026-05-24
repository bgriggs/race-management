using MessagePack;

namespace Cloud.Shared.Telemetry;

/// <summary>
/// Published to the per-team Redis pub/sub change channel when a PerTeam channel
/// value transitions to a new value. Identified by stable ChannelId Guid rather
/// than SessionIndex (which is meaningful only per-car).
/// </summary>
[MessagePackObject]
public class TeamChannelChangeNotification
{
    [Key(0)]
    public Guid ChannelId { get; set; }

    [Key(1)]
    public string Value { get; set; } = string.Empty;

    [Key(2)]
    public DateTime Timestamp { get; set; }
}
