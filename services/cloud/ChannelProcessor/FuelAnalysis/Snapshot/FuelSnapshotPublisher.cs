using System.Globalization;
using ChannelProcessor.FuelAnalysis.ChannelInput;
using Cloud.Shared.FuelAnalysis;
using Cloud.Shared.Streaming;

namespace ChannelProcessor.FuelAnalysis.Snapshot;

/// <summary>
/// Publishes the headline scalars from a <see cref="FuelRangeSnapshot"/> back to the
/// <c>car-channel-values</c> stream as Reserved Channel values per design.md §890 and
/// the channel list at §947. Lap-flavored outputs (RangeLaps, ConsumptionGalPerLap) are
/// skipped until the pace tracker / lap-time aggregator lands.
/// </summary>
public sealed class FuelSnapshotPublisher(ICarChannelPublisher publisher, ILogger<FuelSnapshotPublisher> logger)
{
    public async Task PublishAsync(int teamId, string carNumber, FuelRangeSnapshot snapshot, CancellationToken ct)
    {
        var values = new List<PublishedChannelValue>(8);
        var ts = snapshot.AsOfUtc;

        Add(values, ReservedChannelGuids.FuelRangeMinutes, snapshot.Primary.RangeMinutes, ts);
        Add(values, ReservedChannelGuids.FuelRangeGallons, snapshot.Primary.RangeGallons, ts);
        Add(values, ReservedChannelGuids.FuelRangeLaps, snapshot.Primary.RangeLaps, ts);
        Add(values, ReservedChannelGuids.FuelRangeMinutesHighConf, snapshot.HighConfidence.RangeMinutes, ts);
        Add(values, ReservedChannelGuids.FuelRangeGallonsHighConf, snapshot.HighConfidence.RangeGallons, ts);
        Add(values, ReservedChannelGuids.FuelRangeLapsHighConf, snapshot.HighConfidence.RangeLaps, ts);
        Add(values, ReservedChannelGuids.FuelRangeConfidence, snapshot.Primary.Confidence * 100.0, ts);
        Add(values, ReservedChannelGuids.FuelConsumption, snapshot.Reconciler.CurrentEffectiveRateGalPerMin, ts);
        Add(values, ReservedChannelGuids.FuelConsumptionGalPerLap, ComputeGalPerLap(snapshot), ts);
        Add(values, ReservedChannelGuids.FuelWindowElapsedMinutes, snapshot.FuelWindow.ElapsedMinutes, ts);

        if (values.Count == 0)
        {
            logger.LogDebug("Snapshot for team {TeamId} car {CarNumber} had no publishable headline values", teamId, carNumber);
            return;
        }

        await publisher.PublishAsync(teamId, carNumber, values, ct);
    }

    private static void Add(List<PublishedChannelValue> sink, Guid channelId, double? value, DateTime ts)
    {
        if (value is not double v || double.IsNaN(v) || double.IsInfinity(v)) return;
        sink.Add(new PublishedChannelValue(channelId, v.ToString("R", CultureInfo.InvariantCulture), ts));
    }

    private static double? ComputeGalPerLap(FuelRangeSnapshot snapshot)
    {
        if (snapshot.Reconciler.CurrentEffectiveRateGalPerMin is not double rate) return null;
        if (snapshot.Reconciler.RecentAvgLapTimeSeconds is not double secs || secs <= 0) return null;
        return rate * secs / 60.0;
    }
}
