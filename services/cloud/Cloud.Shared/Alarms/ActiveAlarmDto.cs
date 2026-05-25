namespace Cloud.Shared.Alarms;

/// <summary>
/// Joined view of <c>ActiveAlarms</c> ⇈ <c>AlarmDefinitions</c> for the Race Monitor §3
/// panel. Carries the definition fields the UI needs to render (Name, Message, color,
/// ack-delay) alongside the current state so a single request fills the panel.
/// </summary>
public class ActiveAlarmDto
{
    public int TeamId { get; set; }
    public string CarNumber { get; set; } = string.Empty;
    public Guid AlarmDefinitionId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string DisplayChannelSourceColorHex { get; set; } = "#FFFFFF";
    public int TimeAfterAckToDisplaySecs { get; set; }

    public bool IsActive { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime? LastActivatedAt { get; set; }
    public DateTime? LastAcknowledgedTimestamp { get; set; }
}
