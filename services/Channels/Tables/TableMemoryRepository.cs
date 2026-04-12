namespace Channels.Tables;

public class TableMemoryRepository : ITableRepository
{
    private readonly List<TableMapping> mappings = [];

    public void Add(TableMapping mapping) => mappings.Add(mapping);

    public Task<IEnumerable<TableMapping>> GetMappingsAsync() =>
        Task.FromResult(mappings.AsEnumerable());

    public Task<IEnumerable<TableMapping>> GetDefinitionsAsync() =>
        GetMappingsAsync();
}
