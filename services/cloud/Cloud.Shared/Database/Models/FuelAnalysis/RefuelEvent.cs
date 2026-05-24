using System.ComponentModel.DataAnnotations;

namespace Cloud.Shared.Database.Models.FuelAnalysis;

/// <summary>
/// A detected or manually recorded instant at which fuel was added to a car.
/// Every Refuel Event opens a new <see cref="FuelWindow"/>.
/// </summary>
public class RefuelEvent
{
    public int Id { get; set; }

    public int TeamId { get; set; }
    [StringLength(6, MinimumLength = 1)]
    public required string CarNumber { get; set; }
    public int RaceId { get; set; }

    /// <summary>UTC instant at which the refuel was detected or, for manual entries, the engineer-selected refuel time.</summary>
    public DateTime DetectedAt { get; set; }

    /// <summary>Fuel volume added, in US gallons. Null until the engineer enters it for auto-detected events.</summary>
    public double? EnteredFuelGallons { get; set; }

    /// <summary>UTC instant the engineer entered <see cref="EnteredFuelGallons"/>. Null until entry occurs.</summary>
    public DateTime? EnteredAt { get; set; }

    public RefuelConfidenceTier ConfidenceTier { get; set; }

    /// <summary>Bitmask of <see cref="RefuelAnchor"/> values that contributed to detection.</summary>
    public int AnchorFlags { get; set; }

    public RefuelSource Source { get; set; }

    public EcuResetState EcuResetState { get; set; }

    /// <summary>Lifecycle status — "Pending" until acknowledged, "Confirmed" after volume entry, "Acknowledged-NoEntry" if dismissed without entry.</summary>
    [StringLength(32)]
    public string Status { get; set; } = "Pending";
}

public enum RefuelConfidenceTier { Low, Medium, High, Manual }

public enum RefuelSource { AutoDetected, Manual, SessionStart, InferredOnReconnect }

public enum EcuResetState { Unknown, ResetConfirmed, ResetInferred, Unreset }

[Flags]
public enum RefuelAnchor
{
    None = 0,
    FuelFull = 1,
    FuelLevelRise = 2,
    PitLap = 4,
}
