using Racecar.CanBus;

namespace Racecar.Pipeline.Dispatch;

/// <summary>
/// Receives every raw <see cref="CanMessage"/> before decode. Used for
/// diagnostics, future protocol bridges, and the deferred channel-Logging feature.
/// </summary>
public interface IRawFrameConsumer
{
    string Name { get; }
    RawConsumerOptions Options { get; }
    ValueTask HandleAsync(CanMessage frame, CancellationToken ct);
}
