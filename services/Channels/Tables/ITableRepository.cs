using Channels.Repositories;

namespace Channels.Tables;

public interface ITableRepository : IDefinitionSetRepository<TableDefinition>
{
    public Task<IEnumerable<TableDefinition>> GetMappingsAsync();
}

