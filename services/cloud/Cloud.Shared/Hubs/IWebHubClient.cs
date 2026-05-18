using Cloud.Shared.Telemetry;

namespace Cloud.Shared.Hubs;

/// <summary>
/// Server-to-client methods invoked on connected web clients.
/// </summary>
public interface IWebHubClient
{
    /// <summary>A single channel value changed for one car.</summary>
    Task ChannelValueChanged(string carKey, ChannelChangeNotification change);

    /// <summary>Full snapshot of all channels for every car on the team.</summary>
    Task ChannelSnapshot(CarChannelSnapshot[] cars);
}
