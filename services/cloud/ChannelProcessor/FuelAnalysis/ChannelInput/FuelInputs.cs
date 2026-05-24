namespace ChannelProcessor.FuelAnalysis.ChannelInput;

/// <summary>
/// Typed projection of a stream message's <see cref="Channels.ChannelValue"/> array filtered
/// down to channels the Fuel Reconciler cares about. Each property is null when the
/// corresponding channel did not appear in the message (the upstream change-filter only
/// transmits values that actually changed, so most messages contain only a subset).
/// <para>
/// Boolean-shaped channels (<see cref="FuelFull"/>, <see cref="InPit"/>) follow the
/// project-wide convention of <c>value &gt; 0.5</c> meaning <c>true</c>, mirroring the in-car
/// <see cref="Racecar.FuelAnalysis"/> module.
/// </para>
/// </summary>
public readonly record struct FuelInputs(
    TimestampedDouble? FuelLevel,
    TimestampedDouble? FuelUsed,
    TimestampedDouble? TripFuel,
    TimestampedBool?   FuelFull,
    TimestampedBool?   InPit,
    TimestampedDouble? GpsSpeedMph,
    TimestampedDouble? LastLapTimeSeconds,
    TimestampedDouble? ManualFuelAddedGallons,
    TimestampedDouble? ThrottleProxyFuelUsed,
    TimestampedDouble? ThrottleProxyRate,
    TimestampedDouble? ThrottleProxyConfidence,
    TimestampedDouble? ThrottleProxyGridCoverage)
{
    public bool IsEmpty =>
        FuelLevel is null && FuelUsed is null && TripFuel is null && FuelFull is null
        && InPit is null && GpsSpeedMph is null && LastLapTimeSeconds is null
        && ManualFuelAddedGallons is null
        && ThrottleProxyFuelUsed is null && ThrottleProxyRate is null
        && ThrottleProxyConfidence is null && ThrottleProxyGridCoverage is null;
}

public readonly record struct TimestampedDouble(double Value, DateTime TimestampUtc);
public readonly record struct TimestampedBool(bool Value, DateTime TimestampUtc);
