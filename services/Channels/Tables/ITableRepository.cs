namespace Channels.Tables;

public interface ITableRepository
{
    public Task<IEnumerable<TableMapping>> GetMappingsAsync();
}
