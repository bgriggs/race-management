namespace Common.Racecar;

public class StatusResponse
{
    public string AppVersion { get; set; } = string.Empty;
    public Guid ConfigurationId { get; set; }
    public bool IsCanConnected { get; set; }
    public long CanMessagesReceived { get; set; }
    public bool IsCloudConnected { get; set; }
    public TimeSpan Uptime { get; set; }
}
