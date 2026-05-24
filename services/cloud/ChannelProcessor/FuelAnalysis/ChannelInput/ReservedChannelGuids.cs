namespace ChannelProcessor.FuelAnalysis.ChannelInput;

/// <summary>
/// Compile-time copies of the reserved-channel GUIDs from
/// <see cref="Common.ReservedChannels"/> that the Fuel Reconciler consumes as inputs.
/// Held as <c>static readonly</c> so the per-message dispatch in the worker can compare
/// against them without dictionary lookups. Kept in sync with <c>ReservedChannels.cs</c>
/// by ID — these GUIDs are the cross-tier stable identifiers and never change.
/// </summary>
internal static class ReservedChannelGuids
{
    public static readonly Guid FuelLevel    = Guid.Parse("a2529acf-a7c6-449f-8a85-c7d76b35dbcb");
    public static readonly Guid FuelUsed     = Guid.Parse("740ce2a6-dc88-4425-85dc-7f99f2a902f1");
    public static readonly Guid TripFuel     = Guid.Parse("acd3d127-acaf-4f8a-b27a-8623cfda09f3");
    public static readonly Guid FuelFull     = Guid.Parse("c3b94831-95f6-4935-bf67-1aacfd611f75");
    public static readonly Guid InPit        = Guid.Parse("da12563a-1167-4899-9956-700b0b693005");
    public static readonly Guid GpsSpeed     = Guid.Parse("cf8698bf-3a17-4cb1-b993-c14937ad2fae");
    public static readonly Guid LastLapTime  = Guid.Parse("c354b4ae-64d9-47ee-9f02-ba9c44cf6b74");

    /// <summary>Manual fuel-jug volume entered by an engineer via the WebApi UI. Per ADR-0005, this flows on the same telemetry stream as any other channel value.</summary>
    public static readonly Guid ManualFuelAddedGallons = Guid.Parse("e6c3f1a5-4d2b-4f8e-1c9a-3f5a7d2e4c03");

    // --- In-car Throttle Proxy module outputs (CarToCloud, ManagedByFeature = "throttle-consumption") ---
    public static readonly Guid ThrottleProxyFuelUsed     = Guid.Parse("916d4b4f-5bcf-4d9c-2a1e-4d6e8b3c5a0d");
    public static readonly Guid ThrottleProxyRate         = Guid.Parse("a27e5c50-6cd1-4eac-3b2f-5e7f9c4d6b0e");
    public static readonly Guid ThrottleProxyConfidence   = Guid.Parse("b38f6d61-7de2-4fbd-4c3a-6f8a1d5e7c0f");
    public static readonly Guid ThrottleProxyGridCoverage = Guid.Parse("c49a7e72-8ef3-4ace-5d4b-7a9b2e6f8d10");

    // --- Reconciler headline outputs (CloudLocal, ManagedByFeature = "fuel-analysis") ---
    public static readonly Guid FuelRangeMinutes            = Guid.Parse("f7d4a2b6-5e3c-4a9f-2d1b-4a6b8e3f5d04");
    public static readonly Guid FuelRangeGallons            = Guid.Parse("18e5b3c7-6f4d-4b1a-3e2c-5b7c9f4a6e05");
    public static readonly Guid FuelRangeLaps               = Guid.Parse("29f6c4d8-7a5e-4c2b-4f3d-6c8d1a5b7f06");
    public static readonly Guid FuelRangeMinutesHighConf    = Guid.Parse("3a07d5e9-8b6f-4d3c-5a4e-7d9e2b6c8a07");
    public static readonly Guid FuelRangeGallonsHighConf    = Guid.Parse("4b18e6fa-9c7a-4e4d-6b5f-8e1f3c7d9b08");
    public static readonly Guid FuelRangeLapsHighConf       = Guid.Parse("5c29f70b-1d8b-4f5e-7c6a-9f2a4d8e1c09");
    public static readonly Guid FuelRangeConfidence         = Guid.Parse("6d3a181c-2e9c-4a6f-8d7b-1a3b5e9f2d0a");
    public static readonly Guid FuelConsumption             = Guid.Parse("fcb490e7-e5ef-4b91-99d1-685f81d91112");
    public static readonly Guid FuelConsumptionGalPerLap    = Guid.Parse("7e4b292d-3fad-4b7a-9e8c-2b4c6f1a3e0b");
    public static readonly Guid FuelWindowElapsedMinutes    = Guid.Parse("8f5c3a3e-4abe-4c8b-1f9d-3c5d7a2b4f0c");
}
