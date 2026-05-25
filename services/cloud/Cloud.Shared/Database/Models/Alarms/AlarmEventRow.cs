using System.ComponentModel.DataAnnotations;

namespace Cloud.Shared.Database.Models.Alarms;

public enum AlarmEventType
{
    Activated,
    Deactivated,
    Acknowledged,
}

/// <summary>
/// Append-only history of alarm edge transitions. Written by <c>AlarmProcessorWorker</c>
/// on every state change so the UI can show "alarm fired N times this session" or audit
/// when an engineer acknowledged.
/// </summary>
public class AlarmEventRow
{
    public long Id { get; set; }

    public int TeamId { get; set; }

    [StringLength(6, MinimumLength = 1)]
    public required string CarNumber { get; set; }

    public Guid AlarmDefinitionId { get; set; }

    public AlarmEventType EventType { get; set; }

    public DateTime Timestamp { get; set; }
}
