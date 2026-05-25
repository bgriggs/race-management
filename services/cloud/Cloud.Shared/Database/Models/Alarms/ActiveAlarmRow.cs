using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Cloud.Shared.Database.Models.Alarms;

/// <summary>
/// Current per-(car, alarm) alarm status, upserted by <c>AlarmProcessorWorker</c> after
/// each evaluation. Drives the Race Monitor §3 "Active Alarms" feed without requiring
/// the UI to compute state from the events history.
/// </summary>
[PrimaryKey(nameof(TeamId), nameof(CarNumber), nameof(AlarmDefinitionId))]
public class ActiveAlarmRow
{
    public int TeamId { get; set; }

    [StringLength(6, MinimumLength = 1)]
    public required string CarNumber { get; set; }

    public Guid AlarmDefinitionId { get; set; }

    public bool IsActive { get; set; }
    public bool IsAcknowledged { get; set; }

    public DateTime? LastActivatedAt { get; set; }

    /// <summary>Name aligned with <c>Channels.Alarms.AlarmState.LastAcknowledgedTimestamp</c>.</summary>
    public DateTime? LastAcknowledgedTimestamp { get; set; }
}
