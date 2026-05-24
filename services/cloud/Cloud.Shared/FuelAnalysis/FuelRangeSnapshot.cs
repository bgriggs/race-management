using MessagePack;

namespace Cloud.Shared.FuelAnalysis;

/// <summary>
/// Structured snapshot of the Fuel Reconciler's current output for a single car.
/// Stored in Redis under <see cref="Consts.FUEL_SNAPSHOT_KEY"/> by ChannelProcessor and
/// served as-is to the UI by WebApi for the Race Monitor detail panel (design.md §862–887).
/// Lives in <c>Cloud.Shared</c> so producers (ChannelProcessor) and readers (WebApi) share
/// the contract. MessagePack <c>[MessagePackObject(true)]</c> for schema-evolution forgiveness
/// — adding fields will not break in-flight readers.
/// </summary>
[MessagePackObject(true)]
public sealed class FuelRangeSnapshot
{
    public int TeamId { get; set; }
    public required string CarNumber { get; set; }
    public int RaceId { get; set; }
    public DateTime AsOfUtc { get; set; }

    public required RangeReadout Primary { get; set; }
    public required RangeReadout HighConfidence { get; set; }
    public double HighConfidenceThreshold { get; set; }

    public required List<EstimatorReadout> Estimators { get; set; }
    public required ReconcilerDetails Reconciler { get; set; }
    public required FuelWindowDetails FuelWindow { get; set; }
}

[MessagePackObject(true)]
public sealed class RangeReadout
{
    public double? RangeMinutes { get; set; }
    public double? RangeGallons { get; set; }
    public double? RangeLaps { get; set; }
    /// <summary>One of "blended", "ecu", "flowmeter", "pitfill", "throttle". For HighConfidence this mirrors Primary.</summary>
    public required string Source { get; set; }
    /// <summary>0..1; only meaningful on Primary. HC is a fixed-threshold derivation, not a confidence in its own right.</summary>
    public double Confidence { get; set; }
}

[MessagePackObject(true)]
public sealed class EstimatorReadout
{
    /// <summary>Stable estimator identifier: "ecu", "flowmeter.raw", "flowmeter.corrected", "pitfill", "throttle.integral", "throttle.grid".</summary>
    public required string Name { get; set; }
    public bool Available { get; set; }
    public double? RangeMinutes { get; set; }
    public double? RangeGallons { get; set; }
    public double? RangeLaps { get; set; }
    /// <summary>Half-width sigma on the range value, in gallons. Null when unavailable.</summary>
    public double? Sigma { get; set; }
    public bool IsOutlier { get; set; }
    /// <summary>Human-readable explanation when <see cref="Available"/> is false (e.g., "no Reset-Confirmed ECU").</summary>
    public string? Reason { get; set; }
}

[MessagePackObject(true)]
public sealed class ReconcilerDetails
{
    public double? ConsensusRangeMinutes { get; set; }
    public double? SpreadGallons { get; set; }
    public int OutlierCount { get; set; }
    public double PaceMultiplier { get; set; }
    public double FlagMultiplier { get; set; }
    /// <summary>Rolling mean of recent valid lap times (seconds). Null when no valid laps yet — drives the lap-based outputs and the detail panel's pace explanation.</summary>
    public double? RecentAvgLapTimeSeconds { get; set; }
    public double? CurrentEffectiveRateGalPerMin { get; set; }
    public double? FlowMeterCalibrationFactor { get; set; }
    /// <summary>One of "learned", "manual_override", "reset", or null when no calibration exists.</summary>
    public string? FlowMeterCalibrationFactorSource { get; set; }
}

[MessagePackObject(true)]
public sealed class FuelWindowDetails
{
    public int? Id { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public double ElapsedMinutes { get; set; }
    public double? EnteredFuelGallons { get; set; }
    public double? GallonsUsedInWindow { get; set; }
}
