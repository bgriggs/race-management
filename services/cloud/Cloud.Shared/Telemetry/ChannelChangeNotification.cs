using MessagePack;

namespace Cloud.Shared.Telemetry;

/// <summary>
/// Published to the Redis pub/sub change channel when a channel value transitions to a new value.
/// </summary>
[MessagePackObject]
public class ChannelChangeNotification
{
    [Key(0)]
    public ushort SessionIndex { get; set; }

    [Key(1)]
    public string Value { get; set; } = string.Empty;

    [Key(2)]
    public DateTime Timestamp { get; set; }
}
