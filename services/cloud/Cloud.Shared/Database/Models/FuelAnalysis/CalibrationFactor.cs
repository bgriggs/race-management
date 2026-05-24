using System.ComponentModel.DataAnnotations;

namespace Cloud.Shared.Database.Models.FuelAnalysis;

/// <summary>
/// Insert-only audit log of FlowMeter calibration factor values per car. The current
/// factor is the most-recently-effective row for the (TeamId, CarNumber) pair. Persists
/// across sessions and race events; rebuilt into the reconciler's Redis runtime state on
/// session start.
/// </summary>
public class CalibrationFactor
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    [StringLength(6, MinimumLength = 1)]
    public required string CarNumber { get; set; }

    public double Value { get; set; }

    public CalibrationFactorSource Source { get; set; }

    public DateTime EffectiveAt { get; set; }

    /// <summary>Optional pointer to the race during which the factor was set. Null for off-track manual overrides.</summary>
    public int? RaceId { get; set; }
}

public enum CalibrationFactorSource { Learned, ManualOverride, Reset }
