using Channels.Logic;

namespace Cloud.Shared.Alarms;

/// <summary>
/// Wire shape for an alarm definition. <see cref="CarNumber"/> null marks the row as
/// team-level (applies to every car); non-null marks it as car-only. <see cref="Statement"/>
/// is the strongly-typed graph rather than the raw JSON stored on the EF row, so the UI
/// edits it directly.
/// </summary>
public class AlarmDefinitionDto
{
    public Guid Id { get; set; }

    public int TeamId { get; set; }

    public string? CarNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string DisplayChannelSourceColorHex { get; set; } = "#FFFFFF";

    public int TimeAfterAckToDisplaySecs { get; set; } = 60;

    public Guid? AlarmStatusChannelId { get; set; }

    public StatementDefinition Statement { get; set; } = new();
}
