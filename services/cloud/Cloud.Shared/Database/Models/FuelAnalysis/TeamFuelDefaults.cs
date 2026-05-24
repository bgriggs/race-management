using Microsoft.EntityFrameworkCore;

namespace Cloud.Shared.Database.Models.FuelAnalysis;

/// <summary>
/// Team-level defaults for fuel-analysis parameters that can be overridden per-car on
/// <see cref="Common.FuelAnalysis.CarFuelConfig"/>. Resolution order at runtime is
/// car_override ?? team_default ?? series_default.
/// </summary>
[PrimaryKey(nameof(TeamId))]
public class TeamFuelDefaults
{
    public int TeamId { get; set; }

    public double GreenMultiplier { get; set; } = 1.00;
    public double YellowMultiplier { get; set; } = 0.50;
    public double Code60Multiplier { get; set; } = 0.30;
    public double Code35Multiplier { get; set; } = 0.30;
    public double RedMultiplier { get; set; } = 0.10;

    /// <summary>Confidence threshold used to compute the conservative high-confidence range (0..1). Defaults to 0.98.</summary>
    public double HighConfidenceThreshold { get; set; } = 0.98;
}
