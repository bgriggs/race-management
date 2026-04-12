namespace Channels.Logic;

/// <summary>
/// Abstracts storage of statements. This allows for different implementations of storage, such as in-memory or database, without affecting the logic of statement evaluation.
/// </summary>
public interface IStatementRepository
{
    public Task<Statements> GetStatementsAsync(int statementId);
    public Task SetStatementsAsync(Statements s);
}
