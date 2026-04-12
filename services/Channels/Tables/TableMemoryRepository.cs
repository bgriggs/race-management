namespace Channels.Tables;

public class TableMemoryRepository : ITableRepository
{
    private readonly List<TableDefinition> mappings = [];

    public void Add(TableDefinition mapping) => mappings.Add(mapping);

    public Task<IEnumerable<TableDefinition>> GetMappingsAsync() =>
        Task.FromResult(mappings.AsEnumerable());

    public Task<IEnumerable<TableDefinition>> GetDefinitionsAsync() =>
        GetMappingsAsync();
}

