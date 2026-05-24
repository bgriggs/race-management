using ChannelProcessor.FuelAnalysis.ChannelInput;
using ChannelProcessor.FuelAnalysis.State;
using ChannelProcessor.FuelAnalysis.Windows;
using Cloud.Shared.Database.Models.FuelAnalysis;

namespace ChannelProcessor.FuelAnalysis.Refuel;

/// <summary>
/// Detects Refuel Event anchors from incoming channel values per design.md §814–823:
/// the <c>FuelFull</c> rising edge (with stint-age + InPit/GPSSpeed guards) and the
/// sustained <c>FuelLevel</c> rise while stationary. The pit-lap anchor (RedMist timing
/// data) is deferred to a later slice when the RedMist integration ships.
/// <para>
/// Detection is pure: this class mutates only the edge-tracking fields on
/// <see cref="CarFuelState"/> and returns anchors. DB writes are owned by
/// <see cref="FuelWindowLifecycle"/>.
/// </para>
/// </summary>
public sealed class RefuelEventDetector
{
    private static readonly TimeSpan StintAgeMinForFuelFull = TimeSpan.FromMinutes(15);
    private const double FuelFullGpsSpeedMaxMph = 10.0;
    private const double FuelLevelRiseSpeedMaxMph = 3.0;
    private static readonly TimeSpan FuelLevelRiseSustainedFor = TimeSpan.FromSeconds(5);
    private const double FuelLevelRiseMinGallons = 1.0;

    public IReadOnlyList<DetectedAnchor> Detect(CarFuelState state, FuelInputs inputs)
    {
        // Update last-seen scalars first so anchors that need them (e.g., FuelLevel-rise
        // needs current GPSSpeed) can read from state.
        if (inputs.GpsSpeedMph is TimestampedDouble gps
            && (state.LastGpsSpeedTimestamp is not DateTime gpsLast || gps.TimestampUtc > gpsLast))
        {
            state.LastGpsSpeedMph = gps.Value;
            state.LastGpsSpeedTimestamp = gps.TimestampUtc;
        }
        if (inputs.TripFuel is TimestampedDouble trip
            && (state.LastTripFuelTimestamp is not DateTime tripLast || trip.TimestampUtc > tripLast))
        {
            state.LastTripFuelValue = trip.Value;
            state.LastTripFuelTimestamp = trip.TimestampUtc;
        }
        if (inputs.FuelUsed is TimestampedDouble fuelUsed
            && (state.LastFuelUsedTimestamp is not DateTime fuLast || fuelUsed.TimestampUtc > fuLast))
        {
            state.LastFuelUsedValue = fuelUsed.Value;
            state.LastFuelUsedTimestamp = fuelUsed.TimestampUtc;
        }

        List<DetectedAnchor>? anchors = null;

        if (TryDetectFuelFull(state, inputs) is DetectedAnchor ff)
            (anchors ??= new()).Add(ff);

        if (TryDetectFuelLevelRise(state, inputs) is DetectedAnchor fl)
            (anchors ??= new()).Add(fl);

        return (IReadOnlyList<DetectedAnchor>?)anchors ?? Array.Empty<DetectedAnchor>();
    }

    private static DetectedAnchor? TryDetectFuelFull(CarFuelState state, FuelInputs inputs)
    {
        if (inputs.FuelFull is not TimestampedBool incoming) return null;
        if (state.LastFuelFullTimestamp is DateTime last && incoming.TimestampUtc <= last) return null;

        var was = state.LastFuelFullValue;
        var now = incoming.Value;
        state.LastFuelFullValue = now;
        state.LastFuelFullTimestamp = incoming.TimestampUtc;

        if (was || !now) return null;

        if (state.MostRecentStintStartedAt is not DateTime stintStart) return null;
        if ((incoming.TimestampUtc - stintStart) < StintAgeMinForFuelFull) return null;

        var inPit = inputs.InPit?.Value ?? state.LastInPitValue;
        var gpsSpeed = inputs.GpsSpeedMph?.Value ?? state.LastGpsSpeedMph;
        var slow = gpsSpeed is double s && s < FuelFullGpsSpeedMaxMph;
        if (!inPit && !slow) return null;

        return new DetectedAnchor(RefuelAnchor.FuelFull, incoming.TimestampUtc, inPit || slow);
    }

    private static DetectedAnchor? TryDetectFuelLevelRise(CarFuelState state, FuelInputs inputs)
    {
        if (inputs.FuelLevel is not TimestampedDouble incoming) return null;
        if (state.LastFuelLevelTimestamp is DateTime last && incoming.TimestampUtc <= last) return null;

        var prev = state.LastFuelLevelValue;
        state.LastFuelLevelValue = incoming.Value;
        state.LastFuelLevelTimestamp = incoming.TimestampUtc;

        // Stationary check uses the freshest GPSSpeed (either this message or last known).
        var gpsSpeed = inputs.GpsSpeedMph?.Value ?? state.LastGpsSpeedMph;
        var stationary = gpsSpeed is double s && s < FuelLevelRiseSpeedMaxMph;
        if (!stationary)
        {
            ClearRiseTracker(state);
            return null;
        }

        if (prev is null)
        {
            // No baseline yet — start tracking from here.
            state.FuelLevelRiseStartedAt = incoming.TimestampUtc;
            state.FuelLevelAtRiseStart = incoming.Value;
            return null;
        }

        if (incoming.Value < prev.Value)
        {
            // Going down — reset baseline; this is consumption, not refuel.
            state.FuelLevelRiseStartedAt = incoming.TimestampUtc;
            state.FuelLevelAtRiseStart = incoming.Value;
            return null;
        }

        if (state.FuelLevelRiseStartedAt is null)
        {
            state.FuelLevelRiseStartedAt = incoming.TimestampUtc;
            state.FuelLevelAtRiseStart = prev.Value;
            return null;
        }

        var elapsed = incoming.TimestampUtc - state.FuelLevelRiseStartedAt.Value;
        var delta = incoming.Value - (state.FuelLevelAtRiseStart ?? prev.Value);
        if (elapsed >= FuelLevelRiseSustainedFor && delta >= FuelLevelRiseMinGallons)
        {
            ClearRiseTracker(state);
            var inPit = inputs.InPit?.Value ?? state.LastInPitValue;
            return new DetectedAnchor(RefuelAnchor.FuelLevelRise, incoming.TimestampUtc, inPit || true /* stationary */);
        }

        return null;
    }

    private static void ClearRiseTracker(CarFuelState state)
    {
        state.FuelLevelRiseStartedAt = null;
        state.FuelLevelAtRiseStart = null;
    }
}
