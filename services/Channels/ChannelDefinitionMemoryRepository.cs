namespace Channels;

public class ChannelDefinitionMemoryRepository : IChannelDefinitionRepository
{
    private readonly Dictionary<int, ChannelDefinition> definitions = [];

    public void Set(ChannelDefinition definition) => definitions[definition.Id] = definition;

    public void Set(int id) => definitions[id] = new ChannelDefinition { Id = id };

    public Task<ChannelDefinition> GetChannelDefinitionAsync(int channelId) =>
        Task.FromResult(definitions.TryGetValue(channelId, out var definition)
            ? definition
            : new ChannelDefinition { Id = channelId });

    public Task<ChannelDefinition> GetDefinitionAsync(int id) =>
        GetChannelDefinitionAsync(id);
}
