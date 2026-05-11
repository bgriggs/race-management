namespace Racecar.Pipeline.Dispatch;

/// <summary>
/// Backpressure / overflow modes for raw-frame consumer mailboxes.
/// </summary>
public enum RawConsumerMode
{
    /// <summary>Bounded FIFO. On full, drop the oldest queued frame and increment a drop counter.</summary>
    DropOldest,

    /// <summary>Bounded FIFO. On full, the dispatch attempt fails-fast and is counted (never blocks).</summary>
    Lossless,
}

/// <summary>
/// Backpressure / overflow modes for channel-value consumer mailboxes.
/// </summary>
public enum ChannelConsumerMode
{
    /// <summary>New value for an existing channel ID overwrites unconsumed older value.</summary>
    CoalesceLatestPerChannel,

    /// <summary>Bounded FIFO of batches. On full, drop oldest batch and count.</summary>
    DropOldest,

    /// <summary>Bounded FIFO of batches. On full, dispatch fails-fast and is counted (never blocks).</summary>
    Lossless,
}

public sealed record RawConsumerOptions(
    int Capacity = 256,
    RawConsumerMode Mode = RawConsumerMode.DropOldest);

public sealed record ChannelConsumerOptions(
    int Capacity = 256,
    ChannelConsumerMode Mode = ChannelConsumerMode.CoalesceLatestPerChannel);
