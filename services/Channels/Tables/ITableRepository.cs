using Channels.Repositories;

namespace Channels.Tables;

public interface ITableRepository : IDefinitionSetRepository<TableMapping>
{
    public Task<IEnumerable<TableMapping>> GetMappingsAsync();
}
