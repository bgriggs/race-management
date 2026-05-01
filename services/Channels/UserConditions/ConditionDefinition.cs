using Channels.Logic;

namespace Channels.UserConditions;

/// <summary>
/// User defined set of logic statement from channels that output to another channel as 0 or 1.
/// </summary>
public class ConditionDefinition
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    public List<StatementDefinition> Statements { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifier of the output channel associated with this instance. This will be 0 or 1.
    /// Use <see cref="Guid.Empty"/> when no output channel is needed.
    /// </summary>
    public Guid OutputChannelId { get; set; }
}
