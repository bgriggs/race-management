using MessagePack;

namespace Cloud.Shared.Telemetry;

/// <summary>
/// The latest value for a single channel on a car, persisted as a Redis Hash field value.
/// </summary>
[MessagePackObject]
public class ChannelValueSnapshot
{
    [Key(0)]
    public string Value { get; set; } = string.Empty;

    [Key(1)]
    public DateTime Timestamp { get; set; }
}
