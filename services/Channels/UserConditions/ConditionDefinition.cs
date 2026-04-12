using Channels.Logic;

namespace Channels.UserConditions;

/// <summary>
/// User defined set of logic statement from channels that output to another channel as 0 or 1.
/// </summary>
public class ConditionDefinition
{
    public int Id { get; set; }

    public List<Statements> Statements { get; } = [];

    /// <summary>
    /// Gets or sets the identifier of the output channel associated with this instance. This will be 0 or 1.
    /// </summary>
    public int OutputChannelId { get; set; }
}
