using Channels;

namespace Racecar.Pipeline.Consumers;

/// <summary>
/// Connection target abstraction for <see cref="SignalRTransmitConsumer"/>.
/// Two instances of the consumer (cloud, local pit-laptop) parameterise on
/// different implementations of this interface.
/// </summary>
public interface ISignalRTarget
{
    string Name { get; }

    /// <summary>True if the target is currently connected and ready to receive.</summary>
    bool IsConnected { get; }

    /// <summary>Raised when a previously disconnected target reconnects.</summary>
    event Action? Reconnected;

    /// <summary>Send only the channels that changed since the last delta.</summary>
    Task SendDeltaAsync(IReadOnlyList<ChannelValue> values, CancellationToken ct);

    /// <summary>Send a snapshot of every known channel.</summary>
    Task SendFullAsync(IReadOnlyList<ChannelValue> values, CancellationToken ct);
}
