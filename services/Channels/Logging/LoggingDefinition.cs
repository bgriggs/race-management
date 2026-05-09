namespace Channels.Logging;

public class LoggingDefinition
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }
    public LoggingFrequency Frequency { get; set; }
}

public enum LoggingFrequency
{
    OncePerSecond,
    TwicePerSecond,
    FiveTimesPerSecond,
    TenTimesPerSecond,
    TwentyTimesPerSecond
}