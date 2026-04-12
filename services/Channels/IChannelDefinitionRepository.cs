using Channels.Repositories;

namespace Channels;

/// <summary>
/// Abstracts the saving and loading of channel definitions. This allows for different implementations of storage, such as in-memory or database, without affecting the logic of channel evaluation.
/// </summary>
public interface IChannelDefinitionRepository : IDefinitionReader<int, ChannelDefinition>
{
    public Task<ChannelDefinition> GetChannelDefinitionAsync(int channelId);
}
