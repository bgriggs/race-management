using Channels.Logic;

namespace Channels.Alarms;

public class AlarmDefinition
{
    public Guid Id { get; set; }

    /// <summary>
    /// Statements that control alarm activation state. Each statement can define activate/deactivate comparisons.
    /// </summary>
    public List<StatementDefinition> Statements { get; } = [];

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
    public int TimeAfterAckToDisplaySecs = 60;

    /// <summary>
    /// Optional output channel to write the alarm status to as 0 or 1. This allows for the alarm status to be used in other logic, such as to disable other alarms when this alarm is active.
    /// </summary>
    public Guid? AlarmStatusChannelId { get; set; }
}
