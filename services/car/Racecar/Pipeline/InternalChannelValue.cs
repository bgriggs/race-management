namespace Racecar.Pipeline;

/// <summary>
/// Internal pipeline representation of a single decoded channel value.
/// Carries dual timestamps so the pipeline can use a monotonic clock for
/// interval logic while preserving wall-clock time for transmit/log.
/// </summary>
/// <remarks>
/// This type is internal to the Racecar pipeline. The wire <c>ChannelValue</c>
/// in the <c>Channels</c> project is constructed only at transmit / log boundaries.
/// </remarks>
public readonly record struct InternalChannelValue(
    int ChannelId,
    double BaseValue,
    long MonotonicTicks,
    DateTime WallTime);
