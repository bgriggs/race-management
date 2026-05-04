using Channels.Logic;
using System.ComponentModel.DataAnnotations;

namespace Channels.Alarms;

/// <summary>
/// Model for an alarm definition, which includes the statements that control when the alarm is active, as well as optional display and output settings.
/// </summary>
public class AlarmDefinition
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name. Maximum length is 20 characters.
    /// </summary>
    [MaxLength(20)]
    public required string Name { get; set; }

    /// <summary>
    /// Statement that controls alarm activation state. The statement can define activate/deactivate comparisons.
    /// </summary>
    public StatementDefinition Statement { get; set; } = new();

    /// <summary>
    /// Optional message to display.
    /// </summary>
    public string Messsage { get; set; } = string.Empty;

    /// <summary>
    /// Optional color to make the source channel value on displays, like the dashboard.
    /// </summary>
    public string DisplayChannelSourceColorHex { get; set; } = "#FFFFFF";

    /// <summary>
    /// Specifies the number of seconds to wait before displaying alarm again after it has been acknowledged.
    /// </summary>
    public int TimeAfterAckToDisplaySecs { get; set; } = 60;

    /// <summary>
    /// Optional output channel to write the alarm status to as 0 or 1. This allows for the alarm status to be used in other logic, such as to disable other alarms when this alarm is active.
    /// </summary>
    public Guid? AlarmStatusChannelId { get; set; }
}
