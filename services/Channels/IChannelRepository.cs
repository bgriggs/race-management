using Channels.Repositories;

namespace Channels;

public interface IChannelRepository
{
    Task<ChannelValue> GetChannelValueAsync(Guid channelId);
    Task SetChannelValueAsync(Guid channelId, ChannelValue ch);
}
