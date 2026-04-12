namespace Channels;

public class ChannelMemoryRepository : IChannelRepository
{
    private readonly Dictionary<Guid, ChannelValue> channels = new();
    private static readonly SemaphoreSlim channelsLock = new(1);

    public void Set(Guid id, string value)
    {
        channels[id] = new ChannelValue { Value = value };
    }

    public ChannelValue Get(Guid id)
    {
        return channels.TryGetValue(id, out var v) ? v : new ChannelValue();
    }

    public async Task<ChannelValue> GetChannelValueAsync(Guid channelId)
    {
        await channelsLock.WaitAsync();
        try
        {
            return channels.TryGetValue(channelId, out var value)
                ? value
                : new ChannelValue();
        }
        finally
        {
            channelsLock.Release();
        }
    }

    public async Task SetChannelValueAsync(Guid channelId, ChannelValue ch)
    {
        await channelsLock.WaitAsync();
        try
        {
            channels[channelId] = ch;
        }
        finally
        {
            channelsLock.Release();
        }
    }

    public bool HasChannel(Guid id) => channels.ContainsKey(id);
}