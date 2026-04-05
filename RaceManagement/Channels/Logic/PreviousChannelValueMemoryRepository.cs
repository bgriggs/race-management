namespace Channels.Logic;

public class PreviousChannelValueMemoryRepository : IPreviousChannelValueRepository
{
    private readonly Dictionary<int, string> values = [];
    private static readonly SemaphoreSlim valuesLock = new(1);

    public async Task<string?> GetPreviousValueAsync(int channelId)
    {
        await valuesLock.WaitAsync();
        try
        {
            return values.TryGetValue(channelId, out var value) ? value : null;
        }
        finally
        {
            valuesLock.Release();
        }
    }

    public async Task SetPreviousValueAsync(int channelId, string value)
    {
        await valuesLock.WaitAsync();
        try
        {
            values[channelId] = value;
        }
        finally
        {
            valuesLock.Release();
        }
    }
}
