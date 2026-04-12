namespace Channels.Logic;

public class StatementMemoryRepository : IStatementRepository
{
    private readonly Dictionary<int, StatementDefinition> statementDefinitions = [];

    public void Set(StatementDefinition definition) => statementDefinitions[definition.Id] = definition;

    public void Add(StatementDefinition definition) => statementDefinitions[definition.Id] = definition;

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

    public Task<StatementDefinition> GetDefinitionAsync(int id) =>
        GetStatementDefinitionAsync(id);

    public Task SetDefinitionAsync(StatementDefinition definition) =>
        SetStatementDefinitionAsync(definition);
}
