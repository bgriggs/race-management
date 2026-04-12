namespace Channels;

public class ChannelDefinitionMemoryRepository : IChannelDefinitionRepository
{
    private readonly Dictionary<Guid, ChannelDefinition> definitions = [];

    public void Set(ChannelDefinition definition) => definitions[definition.Id] = definition;

    public void Set(Guid id) => definitions[id] = new ChannelDefinition { Id = id };

    public Task<ChannelDefinition> GetChannelDefinitionAsync(Guid channelId) =>
        Task.FromResult(definitions.TryGetValue(channelId, out var definition)
            ? definition
            : new ChannelDefinition { Id = channelId });

    public Task<ChannelDefinition> GetDefinitionAsync(Guid id) =>
        GetChannelDefinitionAsync(id);
}
