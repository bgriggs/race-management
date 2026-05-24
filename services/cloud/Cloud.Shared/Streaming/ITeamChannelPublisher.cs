using Channels;

namespace Cloud.Shared.Streaming;

/// <summary>
/// Publishes cloud-origin <see cref="TeamChannelValue"/> entries to the
/// <c>team-channel-values</c> Redis Stream for a given team. Unlike
/// <see cref="ICarChannelPublisher"/>, no SessionIndex resolution happens at publish
/// time — the wire payload is keyed by stable ChannelId Guid, and the per-car
/// SessionIndex translation is done by <c>TeamChannelForwardingService</c> at delivery.
/// </summary>
public interface ITeamChannelPublisher
{
    Task PublishAsync(int teamId, TeamChannelValue[] values, CancellationToken ct);
}
