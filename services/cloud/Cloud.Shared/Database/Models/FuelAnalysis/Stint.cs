using System.ComponentModel.DataAnnotations;

namespace Cloud.Shared.Database.Models.FuelAnalysis;

/// <summary>
/// A continuous period of on-track running for a single car between pit stops (or between
/// race start and first pit, or last pit and finish). The fundamental unit of the Fuel
/// &amp; Pit Strategy Gantt visualization. References the <see cref="FuelWindow"/> it belongs
/// to — a single FuelWindow can contain multiple Stints when tire-only pit stops occur.
/// </summary>
public class Stint
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    [StringLength(6, MinimumLength = 1)]
    public required string CarNumber { get; set; }
    public int RaceId { get; set; }

    public int FuelWindowId { get; set; }

    public DateTime StartAt { get; set; }
    public DateTime? EndAt { get; set; }

    /// <summary>Driver identity. Null for v1 — see Fuel Analysis "Non-Goals" in design.md.</summary>
    [StringLength(64)]
    public string? DriverId { get; set; }
}
