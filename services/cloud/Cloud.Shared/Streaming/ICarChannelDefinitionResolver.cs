using Channels;

namespace Cloud.Shared.Streaming;

/// <summary>
/// Resolves the per-car <see cref="ChannelDefinition"/> maps for the car identified by
/// <c>carKey</c> (the field name used on the <c>car-channel-values</c> stream and the
/// key under which the car's active configuration id is tracked in Redis).
/// </summary>
public interface ICarChannelDefinitionResolver
{
    /// <summary>
    /// Returns the SessionIndex → ChannelDefinition map (forward direction). Used by
    /// stream consumers that already have a <see cref="ChannelValue"/> and need to
    /// look up the channel's metadata to make routing decisions. Returns <c>null</c>
    /// when no active configuration is known for the car.
    /// </summary>
    Task<IReadOnlyDictionary<ushort, ChannelDefinition>?> GetSessionIndexMapAsync(string carKey, CancellationToken ct);

    /// <summary>
    /// Returns the ChannelId → SessionIndex reverse map. Used by publishers and
    /// fan-out paths that hold a stable ChannelId Guid and need to translate it to
    /// the receiving car's per-config SessionIndex before writing to the stream
    /// or sending over the hub. Returns <c>null</c> when no active configuration
    /// is known for the car.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ushort>?> GetChannelIdMapAsync(string carKey, CancellationToken ct);
}
