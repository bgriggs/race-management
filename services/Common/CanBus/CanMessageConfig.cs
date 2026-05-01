namespace Common.CanBus;

/// <summary>
/// Represents a CAN packet with a unique 11 or 29 bit identifier.
/// </summary>
public class CanMessageConfig
{
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 11 or 29 bit identifier.
    /// </summary>
    public int CanId { get; set; }

    /// <summary>
    /// True if the message is an extended message 29-bit.
    /// </summary>
    public bool IsExtended { get; set; }

    /// <summary>
    /// 1-8 bytes.
    /// </summary>
    public int Length { get; set; } = 8;
    public bool IsBigEndian { get; set; } = true;
    public bool IsReceive { get; set; } = true;
    public TimeSpan TransmitRate { get; set; } = TimeSpan.FromMilliseconds(1000);

    public List<CanChannelAssignmentConfig> ChannelAssignments { get; set; } = [];
}
