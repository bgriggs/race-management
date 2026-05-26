using ChannelProcessor.FuelAnalysis.ChannelInput;
using Channels;
using Common.FuelAnalysis;

namespace Cloud.Tests.ChannelProcessor.FuelAnalysis;

/// <summary>
/// Verifies that <see cref="FuelChannelExtractor"/> honors the per-car
/// <see cref="CarFuelConfig"/> input bindings introduced in Phase 2 of the channel-routing
/// refactor — picking up custom channel IDs when the user has re-targeted an input, and
/// falling back to reserved-channel defaults when the config is null.
/// </summary>
[TestClass]
public class FuelChannelExtractorTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    // The reserved-channel GUIDs that the extractor falls back to when no config is provided.
    private static readonly Guid ReservedFuelLevel = Guid.Parse("a2529acf-a7c6-449f-8a85-c7d76b35dbcb");
    private static readonly Guid ReservedInPit     = Guid.Parse("da12563a-1167-4899-9956-700b0b693005");

    [TestMethod]
    public void NullConfig_FallsBackToReservedDefaults()
    {
        var (values, map) = BuildSingleValue(ReservedFuelLevel, sessionIndex: 1, value: "12.5");

        var inputs = FuelChannelExtractor.Extract(values, map, fuelConfig: null);

        Assert.IsNotNull(inputs.FuelLevel);
        Assert.AreEqual(12.5, inputs.FuelLevel!.Value.Value);
    }

    [TestMethod]
    public void CustomFuelLevelId_RoutesToFuelLevelInput()
    {
        // User re-targets FuelLevel to a math-computed channel.
        var customFuelLevelId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var config = FreshConfig();
        config.FuelLevelChannelId = customFuelLevelId;

        var (values, map) = BuildSingleValue(customFuelLevelId, sessionIndex: 1, value: "14.2");

        var inputs = FuelChannelExtractor.Extract(values, map, config);

        Assert.IsNotNull(inputs.FuelLevel);
        Assert.AreEqual(14.2, inputs.FuelLevel!.Value.Value);
    }

    [TestMethod]
    public void ReservedFuelLevelId_IgnoredWhenConfigPointsElsewhere()
    {
        // Config binds FuelLevel to a custom channel. An incoming value on the RESERVED channel
        // should NOT populate the FuelLevel input — only the configured channel does.
        var customFuelLevelId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var config = FreshConfig();
        config.FuelLevelChannelId = customFuelLevelId;

        var (values, map) = BuildSingleValue(ReservedFuelLevel, sessionIndex: 1, value: "99.9");

        var inputs = FuelChannelExtractor.Extract(values, map, config);

        Assert.IsNull(inputs.FuelLevel,
            "Reserved-channel value should not bind to FuelLevel input when the config targets a different channel.");
    }

    [TestMethod]
    public void NullInPitChannelId_FallsBackToReservedDefault()
    {
        // Today, null InPitChannelId still falls back to the reserved default (the
        // null-coalescing operator in the extractor). Documented contract — update this
        // assertion if "null = disabled" becomes the intended semantic.
        var config = FreshConfig();
        config.InPitChannelId = null;

        var (values, map) = BuildSingleValue(ReservedInPit, sessionIndex: 1, value: "1");

        var inputs = FuelChannelExtractor.Extract(values, map, config);

        Assert.IsNotNull(inputs.InPit);
        Assert.IsTrue(inputs.InPit!.Value.Value);
    }

    [TestMethod]
    public void AllConfigurableInputs_Routed_FromCustomChannels()
    {
        var customFuelLevel = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var customFuelUsed  = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var customTripFuel  = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var customFuelFull  = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var customInPit     = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var config = FreshConfig();
        config.FuelLevelChannelId = customFuelLevel;
        config.FuelUsedChannelId = customFuelUsed;
        config.TripFuelChannelId = customTripFuel;
        config.FuelFullChannelId = customFuelFull;
        config.InPitChannelId = customInPit;

        var values = new[]
        {
            Cv(sessionIndex: 1, value: "10.0", T0),
            Cv(sessionIndex: 2, value: "3.5",  T0),
            Cv(sessionIndex: 3, value: "0.8",  T0),
            Cv(sessionIndex: 4, value: "1",    T0),  // FuelFull rising
            Cv(sessionIndex: 5, value: "1",    T0),  // InPit
        };
        var map = new Dictionary<ushort, ChannelDefinition>
        {
            [1] = Def(customFuelLevel),
            [2] = Def(customFuelUsed),
            [3] = Def(customTripFuel),
            [4] = Def(customFuelFull),
            [5] = Def(customInPit),
        };

        var inputs = FuelChannelExtractor.Extract(values, map, config);

        Assert.AreEqual(10.0, inputs.FuelLevel?.Value);
        Assert.AreEqual(3.5,  inputs.FuelUsed?.Value);
        Assert.AreEqual(0.8,  inputs.TripFuel?.Value);
        Assert.IsTrue(inputs.FuelFull?.Value);
        Assert.IsTrue(inputs.InPit?.Value);
    }

    [TestMethod]
    public void NonConfigurableInputs_StillReadFromReservedGuids()
    {
        // ManualFuelAddedGallons + ThrottleProxy* outputs aren't on CarFuelConfig; they're
        // always read from the reserved channels regardless of config overrides.
        var manualFuelAdded = Guid.Parse("e6c3f1a5-4d2b-4f8e-1c9a-3f5a7d2e4c03");
        var tpFuelUsed      = Guid.Parse("916d4b4f-5bcf-4d9c-2a1e-4d6e8b3c5a0d");

        // Even with a fully customized config, these still route via the reserved GUIDs.
        var config = FreshConfig();
        config.FuelLevelChannelId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var values = new[]
        {
            Cv(sessionIndex: 1, value: "5.0", T0),
            Cv(sessionIndex: 2, value: "2.0", T0),
        };
        var map = new Dictionary<ushort, ChannelDefinition>
        {
            [1] = Def(manualFuelAdded),
            [2] = Def(tpFuelUsed),
        };

        var inputs = FuelChannelExtractor.Extract(values, map, config);

        Assert.AreEqual(5.0, inputs.ManualFuelAddedGallons?.Value);
        Assert.AreEqual(2.0, inputs.ThrottleProxyFuelUsed?.Value);
    }

    // ---------- Helpers ----------

    private static CarFuelConfig FreshConfig() => new();

    private static ChannelValue Cv(ushort sessionIndex, string value, DateTime timestamp) =>
        new() { SessionIndex = sessionIndex, Value = value, Timestamp = timestamp };

    private static ChannelDefinition Def(Guid id) => new() { Id = id };

    private static (ChannelValue[] Values, IReadOnlyDictionary<ushort, ChannelDefinition> Map)
        BuildSingleValue(Guid channelId, ushort sessionIndex, string value)
    {
        var values = new[] { Cv(sessionIndex, value, T0) };
        var map = new Dictionary<ushort, ChannelDefinition>
        {
            [sessionIndex] = Def(channelId),
        };
        return (values, map);
    }
}
