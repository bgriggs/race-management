/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

/**
 * Where a channel's values are produced and which tiers they are transmitted to.
 * Drives routing: SignalRTransmitConsumer (in-car) skips CarLocal; CarGateway forwards
 * CloudToCar values back to the originating car. Replaces the per-message sendToCar flag.
 */
export enum ChannelDistribution {
    /**
     * Produced on the car; not transmitted to the cloud. In-car-only.
     */
    CarLocal = 0,
    /**
     * Produced on the car; transmitted to the cloud (default for car telemetry).
     */
    CarToCloud = 1,
    /**
     * Produced in the cloud; consumed by cloud services only; not forwarded to the car.
     */
    CloudLocal = 2,
    /**
     * Produced in the cloud; forwarded to the car by CarGateway.
     */
    CloudToCar = 3,
}
