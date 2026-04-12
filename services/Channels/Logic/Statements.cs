namespace Channels.Logic;

/// <summary>
/// Collection of logic comparisons that together form a statement. The statement is true when any of comparisons is true.
/// </summary>
public class Statements
{
    public int Id { get; set; }

    /// <summary>
    /// Rows of comparisons that activate the statement when any comparison is true. This is a list of a list to support grouping comparisons together with AND logic.
    /// </summary>
    public List<List<Comparison>> ActivateComparisons { get; set; } = [];

    /// <summary>
    /// Rows of comparisons that deactivate the statement when any comparison is true. This is a list of a list to support grouping comparisons together with AND logic.
    /// When null, the ActivateComparisons will result in deactivation when false. When not null, the ActivateComparisons will only result in activation when true, and the 
    /// DeactivateComparisons will only result in deactivation when true. 
    /// This allows for more complex logic where a statement can be activated by one set of comparisons and deactivated by another set of comparisons.
    /// </summary>
    public List<List<Comparison>>? DeactivateComparisons { get; set; }
}
