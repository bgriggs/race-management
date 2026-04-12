using Channels.Repositories;

namespace Channels.Logic;

/// <summary>
/// Abstracts storage of statements. This allows for different implementations of storage, such as in-memory or database, without affecting the logic of statement evaluation.
/// </summary>
public interface IStatementRepository : IDefinitionRepository<Guid, StatementDefinition>
{
    public Task<StatementDefinition> GetStatementDefinitionAsync(Guid statementId);
    public Task SetStatementDefinitionAsync(StatementDefinition definition);
}
