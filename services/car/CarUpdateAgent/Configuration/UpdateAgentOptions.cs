namespace CarUpdateAgent.Configuration;

public class UpdateAgentOptions
{
    public const string SectionName = "UpdateAgent";

    /// <summary>Absolute path to the active core app binary on disk.</summary>
    public string CoreAppBinaryPath { get; set; } = "/usr/local/bin/racecar/core-app";

    /// <summary>Absolute path where the previous binary is kept for rollback.</summary>
    public string BackupBinaryPath { get; set; } = "/usr/local/bin/racecar/core-app.prev";

    /// <summary>Absolute path used as the staging area for an incoming binary before swap.</summary>
    public string IncomingBinaryPath { get; set; } = "/tmp/racecar/core-app.incoming";

    /// <summary>systemd unit name for the core app.</summary>
    public string CoreAppSystemdUnit { get; set; } = "racecar-core.service";

    /// <summary>Base URL of the core app's health endpoints (e.g. http://localhost:5000).</summary>
    public string CoreAppHealthBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>Seconds to wait for /health/startup to succeed after an update before rolling back.</summary>
    public int WatchdogStartupTimeoutSeconds { get; set; } = 30;

    /// <summary>Seconds to continue polling /health/live after startup succeeds before declaring success.</summary>
    public int WatchdogLivenessWindowSeconds { get; set; } = 30;

    /// <summary>Full URL of the cloud SignalR hub (e.g. https://api.example.com/hubs/updates).</summary>
    public string CloudHubUrl { get; set; } = string.Empty;
}
