using Cloud.Shared.Database.Models.Alarms;
using MessagePack;

namespace Cloud.Shared.Alarms;

/// <summary>
/// Payload that travels over both Redis pub/sub (between ChannelProcessor and WebApi)
/// and SignalR (from WebApi to browsers). Carries the minimum needed for the Race
/// Monitor §3 Active Alarms panel to update one row without re-fetching.
/// </summary>
[MessagePackObject]
public class AlarmChangeNotification
{
    [Key(0)]
    public int TeamId { get; set; }

    [Key(1)]
    public string CarNumber { get; set; } = string.Empty;

    [Key(2)]
    public Guid AlarmDefinitionId { get; set; }

    [Key(3)]
    public AlarmEventType EventType { get; set; }

    [Key(4)]
    public bool IsActive { get; set; }

    [Key(5)]
    public bool IsAcknowledged { get; set; }

    [Key(6)]
    public DateTime Timestamp { get; set; }
}
