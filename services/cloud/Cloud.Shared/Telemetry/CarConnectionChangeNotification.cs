using MessagePack;

namespace Cloud.Shared.Telemetry;

/// <summary>
/// Published on the CAR_CONNECTION_CHANGES_CHANNEL pub/sub channel when a car's
/// CarHub connection comes up or goes down. The UI uses this to colour the
/// car-status dot independently from telemetry recency.
/// </summary>
[MessagePackObject]
public class CarConnectionChangeNotification
{
    [Key(0)]
    public string CarKey { get; set; } = string.Empty;

    [Key(1)]
    public bool IsConnected { get; set; }

    [Key(2)]
    public DateTime Timestamp { get; set; }
}
