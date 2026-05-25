using Cloud.Shared.Alarms;
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

    /// <summary>A single alarm transitioned (Activated, Deactivated, or Acknowledged).</summary>
    Task AlarmChanged(AlarmChangeNotification change);

    /// <summary>Full snapshot of active alarms for the team — drives the Race Monitor §3 panel.</summary>
    Task AlarmSnapshot(ActiveAlarmDto[] alarms);
}
