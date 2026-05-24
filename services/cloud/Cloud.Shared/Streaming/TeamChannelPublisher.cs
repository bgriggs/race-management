using Channels;
using MessagePack;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Cloud.Shared.Streaming;

public class TeamChannelPublisher(
    IConnectionMultiplexer redis,
    ILogger<TeamChannelPublisher> logger) : ITeamChannelPublisher
{
    public async Task PublishAsync(int teamId, TeamChannelValue[] values, CancellationToken ct)
    {
        if (values.Length == 0) return;

        var teamField = string.Format(Consts.TEAM_STREAM_FIELD, teamId);
        var payload = MessagePackSerializer.Serialize(values, cancellationToken: ct);

        var db = redis.GetDatabase();
        await db.StreamAddAsync(Consts.TEAM_CHANNEL_VALUES_STREAM_KEY, teamField, payload);

        logger.LogDebug("Published {Count} team channel values to team {TeamId}", values.Length, teamId);
    }
}
