using Channels;

namespace ChannelProcessor.FuelAnalysis.ChannelInput;

/// <summary>
/// Projects a <see cref="ChannelValue"/> array (one stream message's payload) into a
/// <see cref="FuelInputs"/> struct, mapping per-config <c>SessionIndex</c> values back to
/// the reserved-channel GUIDs the reconciler reasons about. When a message carries
/// multiple updates of the same channel (rare, but possible if a value flapped within the
/// same change-filter window), the latest-by-timestamp wins.
/// </summary>
public static class FuelChannelExtractor
{
    public static FuelInputs Extract(
        ReadOnlySpan<ChannelValue> values,
        IReadOnlyDictionary<ushort, ChannelDefinition> sessionIndexMap)
    {
        TimestampedDouble? fuelLevel = null;
        TimestampedDouble? fuelUsed = null;
        TimestampedDouble? tripFuel = null;
        TimestampedBool? fuelFull = null;
        TimestampedBool? inPit = null;
        TimestampedDouble? gpsSpeed = null;
        TimestampedDouble? lastLap = null;
        TimestampedDouble? manualFuelAdded = null;
        TimestampedDouble? tpFuelUsed = null;
        TimestampedDouble? tpRate = null;
        TimestampedDouble? tpConfidence = null;
        TimestampedDouble? tpGridCoverage = null;

        for (var i = 0; i < values.Length; i++)
        {
            var cv = values[i];
            if (!sessionIndexMap.TryGetValue(cv.SessionIndex, out var def)) continue;
            var id = def.Id;

            if (id == ReservedChannelGuids.FuelLevel)
                fuelLevel = TakeLatest(fuelLevel, new TimestampedDouble(cv.GetValueDouble(), cv.Timestamp));
            else if (id == ReservedChannelGuids.FuelUsed)
                fuelUsed = TakeLatest(fuelUsed, new TimestampedDouble(cv.GetValueDouble(), cv.Timestamp));
            else if (id == ReservedChannelGuids.TripFuel)
                tripFuel = TakeLatest(tripFuel, new TimestampedDouble(cv.GetValueDouble(), cv.Timestamp));
            else if (id == ReservedChannelGuids.FuelFull)
                fuelFull = TakeLatest(fuelFull, new TimestampedBool(cv.GetValueDouble() > 0.5, cv.Timestamp));
            else if (id == ReservedChannelGuids.InPit)
                inPit = TakeLatest(inPit, new TimestampedBool(cv.GetValueDouble() > 0.5, cv.Timestamp));
            else if (id == ReservedChannelGuids.GpsSpeed)
                gpsSpeed = TakeLatest(gpsSpeed, new TimestampedDouble(cv.GetValueDouble(), cv.Timestamp));
            else if (id == ReservedChannelGuids.LastLapTime)
                lastLap = TakeLatest(lastLap, new TimestampedDouble(cv.GetValueDouble(), cv.Timestamp));
            else if (id == ReservedChannelGuids.ManualFuelAddedGallons)
                manualFuelAdded = TakeLatest(manualFuelAdded, new TimestampedDouble(cv.GetValueDouble(), cv.Timestamp));
            else if (id == ReservedChannelGuids.ThrottleProxyFuelUsed)
                tpFuelUsed = TakeLatest(tpFuelUsed, new TimestampedDouble(cv.GetValueDouble(), cv.Timestamp));
            else if (id == ReservedChannelGuids.ThrottleProxyRate)
                tpRate = TakeLatest(tpRate, new TimestampedDouble(cv.GetValueDouble(), cv.Timestamp));
            else if (id == ReservedChannelGuids.ThrottleProxyConfidence)
                tpConfidence = TakeLatest(tpConfidence, new TimestampedDouble(cv.GetValueDouble(), cv.Timestamp));
            else if (id == ReservedChannelGuids.ThrottleProxyGridCoverage)
                tpGridCoverage = TakeLatest(tpGridCoverage, new TimestampedDouble(cv.GetValueDouble(), cv.Timestamp));
        }

        return new FuelInputs(fuelLevel, fuelUsed, tripFuel, fuelFull, inPit, gpsSpeed, lastLap, manualFuelAdded,
            tpFuelUsed, tpRate, tpConfidence, tpGridCoverage);
    }

    private static TimestampedDouble? TakeLatest(TimestampedDouble? current, TimestampedDouble incoming) =>
        current is null || incoming.TimestampUtc >= current.Value.TimestampUtc ? incoming : current;

    private static TimestampedBool? TakeLatest(TimestampedBool? current, TimestampedBool incoming) =>
        current is null || incoming.TimestampUtc >= current.Value.TimestampUtc ? incoming : current;
}
