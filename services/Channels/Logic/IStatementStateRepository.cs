namespace Channels.Logic;

/// <summary>
/// Abstracts persistence of statement active/inactive state, enabling retain-state logic
/// when both activate and deactivate comparisons are defined.
/// </summary>
public interface IStatementStateRepository
{
    /// <summary>Returns the current state for the statement, or null if no state has been recorded.</summary>
    Task<bool?> GetStateAsync(Guid statementId);

    Task SetStateAsync(Guid statementId, bool state);
}
