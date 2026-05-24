namespace Cloud.Shared.Streaming;

/// <summary>
/// Input shape for <see cref="ICarChannelPublisher.PublishAsync"/>. Producers identify
/// channels by their stable Guid; the publisher resolves each to the target car's
/// per-config SessionIndex before writing to the stream.
/// </summary>
public readonly record struct PublishedChannelValue(Guid ChannelId, string Value, DateTime Timestamp);

/// <summary>
/// Publishes cloud-origin <see cref="Channels.ChannelValue"/> entries to the
/// <c>car-channel-values</c> Redis Stream targeted at a specific car. The publisher
/// resolves each input's <see cref="PublishedChannelValue.ChannelId"/> to the car's
/// current SessionIndex via <see cref="ICarChannelDefinitionResolver"/>; values whose
/// channels are not in the car's active configuration are dropped (with a warning log)
/// rather than block the rest of the batch.
/// </summary>
public interface ICarChannelPublisher
{
    /// <summary>
    /// Serializes and writes the given values to the <c>car-channel-values</c> stream
    /// for the specified <c>(teamId, car)</c> pair.
    /// </summary>
    /// <returns>The number of values successfully resolved and included in the published
    /// payload. Returns <c>0</c> when the car has no active configuration in Redis, or
    /// when every value's channel id is missing from that configuration.</returns>
    Task<int> PublishAsync(int teamId, string car, IReadOnlyList<PublishedChannelValue> values, CancellationToken ct);
}
