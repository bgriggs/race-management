using System.ComponentModel.DataAnnotations;

namespace Cloud.Shared.Database.Models.FuelAnalysis;

/// <summary>
/// A continuous period between two <see cref="RefuelEvent"/>s (or between session start
/// and the first Refuel Event, or the last Refuel Event and session end). Contains one
/// or more <see cref="Stint"/>s — a tire-only pit stop ends a Stint but not a FuelWindow.
/// The unit fuel-consumption math operates on.
/// </summary>
public class FuelWindow
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    [StringLength(6, MinimumLength = 1)]
    public required string CarNumber { get; set; }
    public int RaceId { get; set; }

    public int StartRefuelEventId { get; set; }
    public int? EndRefuelEventId { get; set; }

    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    /// <summary>Observed consumption rate over the closed window. Null while the window is open.</summary>
    public double? ObservedConsumptionGalPerMin { get; set; }
    /// <summary>Wall-clock duration of the closed window in seconds. Null while open.</summary>
    public double? ObservedDurationSeconds { get; set; }

    /// <summary>True when the window was force-closed by session end rather than a real Refuel Event — excluded from calibration learning.</summary>
    public bool ClosedBySessionEnd { get; set; }
}
