using Cloud.Shared.Alarms;
using Cloud.Shared.RedMist;
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

    /// <summary>
    /// Latest RedMist-sourced race-header state (race time, time-to-go, leader lap, flag).
    /// <c>null</c> means the team has no active RedMist session — the client should render
    /// blanks rather than fall back to local clocks.
    /// </summary>
    Task RaceStateChanged(RaceStateDto? state);

    /// <summary>
    /// A single car-hub connection transitioned (connected or disconnected). The UI uses
    /// this to colour the per-car status indicator independently from telemetry recency.
    /// </summary>
    Task CarConnectionChanged(CarConnectionChangeNotification change);

    /// <summary>
    /// Snapshot of every carKey currently connected to CarHub for the subscribed team,
    /// sent on SubscribeToTeam so the UI can seed its connection state without waiting
    /// for the first per-car change event.
    /// </summary>
    Task CarConnectionSnapshot(string[] connectedCarKeys);
}
