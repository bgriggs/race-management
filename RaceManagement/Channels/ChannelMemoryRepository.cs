namespace Channels;

public class ChannelMemoryRepository : IChannelRepository
{
    private readonly Dictionary<int, ChannelValue> channels = new();
    private static readonly SemaphoreSlim channelsLock = new(1);

    public async Task<ChannelValue> GetChannelValueAsync(int channelId)
    {
        await channelsLock.WaitAsync();
        try
        {
            return channels[channelId];
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
}