namespace Cloud.Shared.Alarms;

/// <summary>
/// Reads the joined <c>ActiveAlarms</c> ⇈ <c>AlarmDefinitions</c> view for a team.
/// Used both by the WebApi controller (GET active feed) and the SignalR periodic
/// snapshot loop so both paths emit the same shape.
/// </summary>
public interface IActiveAlarmsReader
{
    Task<ActiveAlarmDto[]> GetForTeamAsync(int teamId, bool includeAcknowledged, CancellationToken ct = default);
}
