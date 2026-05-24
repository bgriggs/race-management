namespace Channels;

/// <summary>
/// Where a channel's values are produced and which tiers they are transmitted to.
/// Drives routing: SignalRTransmitConsumer (in-car) skips CarLocal; CarGateway forwards
/// CloudToCar values back to the originating car. Replaces the per-message sendToCar flag.
/// </summary>
public enum ChannelDistribution
{
    /// <summary>Produced on the car; not transmitted to the cloud. In-car-only.</summary>
    CarLocal = 0,

    /// <summary>Produced on the car; transmitted to the cloud (default for car telemetry).</summary>
    CarToCloud = 1,

    /// <summary>Produced in the cloud; consumed by cloud services only; not forwarded to the car.</summary>
    CloudLocal = 2,

    /// <summary>Produced in the cloud; forwarded to the car by CarGateway.</summary>
    CloudToCar = 3,
}
