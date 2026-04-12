namespace Channels;

public class ChannelMemoryRepository : IChannelRepository
{
    private readonly Dictionary<int, ChannelValue> channels = new();
    private static readonly SemaphoreSlim channelsLock = new(1);

    public void Set(int id, string value)
    {
        channels[id] = new ChannelValue { Id = id, Value = value };
    }

    public ChannelValue Get(int id)
    {
        return channels.TryGetValue(id, out var v) ? v : new ChannelValue { Id = id };
    }

    public async Task<ChannelValue> GetChannelValueAsync(int channelId)
    {
        await channelsLock.WaitAsync();
        try
        {
            return channels.TryGetValue(channelId, out var value)
                ? value
                : new ChannelValue { Id = channelId };
        }
        finally
        {
            channelsLock.Release();
        }
    }

    public async Task SetChannelValueAsync(ChannelValue ch)
    {
        await channelsLock.WaitAsync();
        try
        {
            channels[ch.Id] = ch;
        }
        finally
        {
            channelsLock.Release();
        }
    }

    public Task<ChannelValue> GetStateAsync(int id) =>
        GetChannelValueAsync(id);

    public Task SetStateAsync(ChannelValue state) =>
        SetChannelValueAsync(state);

    public bool HasChannel(int id) => channels.ContainsKey(id);
}