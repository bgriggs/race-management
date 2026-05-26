using System.ComponentModel.DataAnnotations;

namespace Common.FuelAnalysis;

public class ThrottleConsumptionConfig
{
    public bool IsEnabled { get; set; }
    [Range(2000, 12000)]
    public int MaxRpm { get; set; } = 7000;

    // Throttle-specific input bindings. Fuel-side signals (TripFuel, FuelUsed, FuelFull, InPit)
    // live on the parent CarFuelConfig — they're declared once and read by both the cloud
    // FuelReconciler and the in-car ThrottleProxyConsumer.

    /// <summary>Source channel for the throttle-position signal (default: ThrottlePosition reserved channel).</summary>
    public Guid ThrottlePositionChannelId { get; set; } = Guid.Parse("c4a1f8e3-2b9d-4f6c-8a7e-1d3e5b9c2a01");

    /// <summary>Source channel for engine RPM (default: EngineRPM reserved channel).</summary>
    public Guid EngineRpmChannelId { get; set; } = Guid.Parse("74c57a58-d78d-499a-977b-11cee221926a");
}
