using System;
using System.Collections.Generic;
using System.Text;

namespace Channels.Logic;

public class StatementMemoryRepository : IStatementRepository
{
    private readonly Dictionary<int, StatementDefinition> statementDefinitions = [];

    public Task<StatementDefinition> GetStatementDefinitionAsync(int statementId)
    {
        _ = statementDefinitions.TryGetValue(statementId, out StatementDefinition? definition);
        definition ??= new StatementDefinition { Id = statementId };
        return Task.FromResult(definition);
    }

    public Task SetStatementDefinitionAsync(StatementDefinition definition)
    {
        statementDefinitions[definition.Id] = definition;
        return Task.CompletedTask;
    }
}
