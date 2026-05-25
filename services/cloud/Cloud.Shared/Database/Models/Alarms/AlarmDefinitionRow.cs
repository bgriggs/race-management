using System.ComponentModel.DataAnnotations;

namespace Cloud.Shared.Database.Models.Alarms;

/// <summary>
/// Persisted alarm definition. Mirrors <c>Channels.Alarms.AlarmDefinition</c> fields with
/// the nested <c>StatementDefinition</c> graph serialized to <see cref="StatementJson"/>
/// (jsonb). <see cref="CarNumber"/> null means the definition is team-level and applies
/// to every car on the team; non-null means car-only. Team and car definitions evaluate
/// independently (no suppression link).
/// </summary>
public class AlarmDefinitionRow
{
    public Guid Id { get; set; }

    public int TeamId { get; set; }

    [StringLength(6, MinimumLength = 1)]
    public string? CarNumber { get; set; }

    [StringLength(20, MinimumLength = 1)]
    public required string Name { get; set; }

    public string Message { get; set; } = string.Empty;

    [StringLength(7)]
    public string DisplayChannelSourceColorHex { get; set; } = "#FFFFFF";

    public int TimeAfterAckToDisplaySecs { get; set; } = 60;

    public Guid? AlarmStatusChannelId { get; set; }

    /// <summary>Serialized <c>Channels.Logic.StatementDefinition</c> graph.</summary>
    public string StatementJson { get; set; } = "{}";
}
