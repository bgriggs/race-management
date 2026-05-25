using Channels;

namespace ChannelProcessor.Alarms.Channel;

/// <summary>
/// In-memory <see cref="IChannelDefinitionRepository"/> over a car's session-index
/// definition map. Constructed once per alarm evaluation pass so the underlying map
/// snapshot (resolved from the car's active configuration) is stable for the duration.
/// Returns an empty <see cref="ChannelDefinition"/> for unknown ids — matches the
/// memory repository's defensive behaviour rather than throwing into the evaluator.
/// </summary>
public sealed class CarScopedChannelDefinitionRepository : IChannelDefinitionRepository
{
    private readonly Dictionary<Guid, ChannelDefinition> _byId;

    public CarScopedChannelDefinitionRepository(IReadOnlyDictionary<ushort, ChannelDefinition> sessionMap)
    {
        _byId = new Dictionary<Guid, ChannelDefinition>(sessionMap.Count);
        foreach (var def in sessionMap.Values)
        {
            _byId[def.Id] = def;
        }
    }

    public Task<ChannelDefinition> GetChannelDefinitionAsync(Guid channelId)
        => Task.FromResult(_byId.TryGetValue(channelId, out var def)
            ? def
            : new ChannelDefinition { Id = channelId });

    public Task<ChannelDefinition> GetDefinitionAsync(Guid id) => GetChannelDefinitionAsync(id);
}
