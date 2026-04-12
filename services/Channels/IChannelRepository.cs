using Channels.Repositories;

namespace Channels;

public interface IChannelRepository : IStateRepository<int, ChannelValue>
{
    public Task<ChannelValue> GetChannelValueAsync(int channelId);
    public Task SetChannelValueAsync(ChannelValue ch);
}
