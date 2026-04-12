namespace Channels.Logic;

public class ComparisonDefinition
{
    public int Id { get; set; }
    public Guid ChannelId { get; set; }
    public LogicType Logic { get; set; }
    public bool UseStaticComparison { get; set; }
    public string StaticValueComparison { get; set; } = string.Empty;

    /// <summary>
    ///  When not using static comparison, this is the channel to compare to. The value of the channel specified by ChannelId will be compared to the value of the channel specified by ChannelComparisonId.
    /// </summary>
    public Guid? ChannelComparisonId { get; set; }
    /// <summary>
    /// Amount of time in milliseconds the statement must be true before the statement is considered true when using the 'on for' clause.
    /// </summary>
    public int ForMs { get; set; }
    public bool ReverseResult { get; set; }
}
