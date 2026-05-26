using System.ComponentModel.DataAnnotations;

namespace Common.FuelAnalysis;

public class CarFuelConfig
{
    public bool IsEnabled { get; set; }

    [Range(1, 100)]
    public double TankCapacityGallons { get; set; }
    [Range(0.0001, 10)]
    public double DefaultConsumptionGalPerMin { get; set; }
    [Range(0, 1)]
    public double DefaultYellowConsumptionMultiplier { get; set; } = 0.5;
    [Range(0, 1)]
    public double DefaultCode35ConsumptionMultiplier { get; set; } = 0.3;

    // Fuel-signal channel bindings. Read by the cloud FuelReconciler and the in-car
    // ThrottleProxyConsumer. Defaults point at the matching reserved channels; the user
    // can re-target any of these in the Fuel Analysis config UI.

    /// <summary>Source channel for tank fuel level (default: FuelLevel reserved channel). Used by the FuelReconciler's tank-level estimator.</summary>
    public Guid FuelLevelChannelId { get; set; } = Guid.Parse("a2529acf-a7c6-449f-8a85-c7d76b35dbcb");

    /// <summary>Source channel for trip fuel (default: TripFuel reserved channel).</summary>
    public Guid TripFuelChannelId { get; set; } = Guid.Parse("acd3d127-acaf-4f8a-b27a-8623cfda09f3");

    /// <summary>Source channel for cumulative fuel used (default: FuelUsed reserved channel).</summary>
    public Guid FuelUsedChannelId { get; set; } = Guid.Parse("740ce2a6-dc88-4425-85dc-7f99f2a902f1");

    /// <summary>Source channel for the fuel-full reset signal (default: FuelFull reserved channel).</summary>
    public Guid FuelFullChannelId { get; set; } = Guid.Parse("c3b94831-95f6-4935-bf67-1aacfd611f75");

    /// <summary>Optional source channel for the pit-lane indicator (default: InPit reserved channel; null disables pit gating in the throttle proxy).</summary>
    public Guid? InPitChannelId { get; set; } = Guid.Parse("da12563a-1167-4899-9956-700b0b693005");

    public ThrottleConsumptionConfig ThrottleConsumption { get; set; } = new();
}
