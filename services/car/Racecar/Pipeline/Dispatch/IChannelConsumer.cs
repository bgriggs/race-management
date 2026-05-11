namespace Racecar.Pipeline.Dispatch;

/// <summary>
/// Receives decoded, change-filtered, derived channel-value batches dispatched
/// by channel ID.
/// </summary>
public interface IChannelConsumer
{
    string Name { get; }
    ChannelConsumerOptions Options { get; }

    /// <summary>
    /// Returns the set of channel IDs this consumer is interested in for the
    /// given configuration, or <c>null</c> to subscribe to all channels.
    /// </summary>
    IReadOnlySet<int>? GetSubscriptions(ActiveConfiguration config);

    ValueTask HandleAsync(ReadOnlyMemory<InternalChannelValue> values, CancellationToken ct);
}
