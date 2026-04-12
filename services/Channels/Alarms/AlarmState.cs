namespace Channels.Alarms;

public class AlarmState
{
    public Guid Id { get; set; }

    public bool IsActive { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime LastAcknowledgedTimestamp { get; set; }
}
