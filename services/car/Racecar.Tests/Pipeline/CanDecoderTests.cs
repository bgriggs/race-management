using Channels;
using Racecar.CanBus;
using Racecar.Pipeline;

namespace Racecar.Tests.Pipeline;

[TestClass]
public sealed class CanDecoderTests
{
    private static ActiveConfiguration ConfigFor(CanMessageMapping mapping)
    {
        return ActiveConfiguration.Empty with
        {
            Messages = new Dictionary<(int, uint), CanMessageMapping>
            {
                [(mapping.CanBusIndex, mapping.CanId)] = mapping,
            },
            Channels = mapping.Channels.ToDictionary(c => c.ChannelId, _ => new ChannelDefinition()),
        };
    }

    [TestMethod]
    public void Returns_zero_for_unmapped_id()
    {
        var decoder = new CanDecoder();
        var config = ActiveConfiguration.Empty;
        var frame = new TimestampedFrame(0,
            new CanMessage { CanId = 0x100, Data = [0, 0], DataLength = 2 },
            0, default);

        var span = new InternalChannelValue[4].AsSpan();
        var n = decoder.Decode(config, in frame, span);
        Assert.AreEqual(0, n);
    }

    [TestMethod]
    public void Decodes_unsigned_big_endian_with_formula()
    {
        var decoder = new CanDecoder();
        var ch = new CanChannelMapping(
            ChannelId: 1, Offset: 0, Length: 2, Mask: 0xFFFF,
            IsSigned: false, IsBigEndian: true,
            FormulaMultiplier: 0.1, FormulaDivider: 1, FormulaConst: -40);
        var msg = new CanMessageMapping(0, 0x123, Length: 8, IsBigEndian: true,
            Channels: [ch]);
        var config = ConfigFor(msg);

        // raw = 0x0190 = 400 -> 400 * 0.1 - 40 = 0
        var frame = new TimestampedFrame(0,
            new CanMessage
            {
                CanId = 0x123,
                Data = [0x01, 0x90, 0, 0, 0, 0, 0, 0],
                DataLength = 8,
            },
            MonotonicTicks: 1234, WallTime: new DateTime(2026, 5, 10));

        var output = new InternalChannelValue[4];
        var n = decoder.Decode(config, in frame, output);

        Assert.AreEqual(1, n);
        Assert.AreEqual(1, output[0].ChannelId);
        Assert.AreEqual(0.0, output[0].Value, 1e-9);
        Assert.AreEqual(1234, output[0].MonotonicTicks);
    }

    [TestMethod]
    public void Sign_extends_signed_channel()
    {
        var decoder = new CanDecoder();
        var ch = new CanChannelMapping(
            ChannelId: 7, Offset: 0, Length: 1, Mask: 0xFF,
            IsSigned: true, IsBigEndian: true,
            FormulaMultiplier: 1, FormulaDivider: 1, FormulaConst: 0);
        var config = ConfigFor(new CanMessageMapping(0, 0x1, 1, true, [ch]));

        // 0xFF signed -> -1
        var frame = new TimestampedFrame(0,
            new CanMessage { CanId = 1, Data = [0xFF], DataLength = 1 },
            0, default);
        var output = new InternalChannelValue[1];
        _ = decoder.Decode(config, in frame, output);
        Assert.AreEqual(-1.0, output[0].Value);
    }

    [TestMethod]
    public void Applies_unit_conversion_from_config()
    {
        var decoder = new CanDecoder();
        // Channel 10: raw byte value will be treated as °C, converted to °F.
        // raw 0x00 -> formula 0.0 °C -> 32.0 °F
        var ch = new CanChannelMapping(
            ChannelId: 10, Offset: 0, Length: 1, Mask: 0xFF,
            IsSigned: false, IsBigEndian: true,
            FormulaMultiplier: 1, FormulaDivider: 1, FormulaConst: 0);
        var msg = new CanMessageMapping(0, 0x200, Length: 1, IsBigEndian: true, Channels: [ch]);

        var def = new ChannelDefinition
        {
            DataType = "Temperature",
            BaseUnitType = "DegreeCelsius",
            OutputUnitType = "DegreeFahrenheit",
            LowRange = -100,
            HighRange = 300,
        };
        var converter = ChannelUnitConverter.Build(def, out _);

        var config = ActiveConfiguration.Empty with
        {
            Messages = new Dictionary<(int, uint), CanMessageMapping>
            {
                [(0, 0x200u)] = msg,
            },
            Channels = new Dictionary<int, ChannelDefinition> { [10] = def },
            UnitConverters = new Dictionary<int, ChannelUnitConverter> { [10] = converter },
        };

        var frame = new TimestampedFrame(0,
            new CanMessage { CanId = 0x200, Data = [0x00], DataLength = 1 },
            0, default);

        var output = new InternalChannelValue[1];
        var n = decoder.Decode(config, in frame, output);

        Assert.AreEqual(1, n);
        Assert.AreEqual(32.0, output[0].Value, 1e-6); // 0 °C → 32 °F
    }

    [TestMethod]
    public void Clamps_value_to_HighRange_after_conversion()
    {
        var decoder = new CanDecoder();
        // raw 0x96 = 150 -> formula 150 °C -> 302 °F, clamped to HighRange 250 °F
        var ch = new CanChannelMapping(
            ChannelId: 11, Offset: 0, Length: 1, Mask: 0xFF,
            IsSigned: false, IsBigEndian: true,
            FormulaMultiplier: 1, FormulaDivider: 1, FormulaConst: 0);
        var msg = new CanMessageMapping(0, 0x201, Length: 1, IsBigEndian: true, Channels: [ch]);

        var def = new ChannelDefinition
        {
            DataType = "Temperature",
            BaseUnitType = "DegreeCelsius",
            OutputUnitType = "DegreeFahrenheit",
            LowRange = -100,
            HighRange = 250,
        };
        var converter = ChannelUnitConverter.Build(def, out _);

        var config = ActiveConfiguration.Empty with
        {
            Messages = new Dictionary<(int, uint), CanMessageMapping>
            {
                [(0, 0x201u)] = msg,
            },
            Channels = new Dictionary<int, ChannelDefinition> { [11] = def },
            UnitConverters = new Dictionary<int, ChannelUnitConverter> { [11] = converter },
        };

        var frame = new TimestampedFrame(0,
            new CanMessage { CanId = 0x201, Data = [0x96], DataLength = 1 }, // 150 decimal
            0, default);

        var output = new InternalChannelValue[1];
        _ = decoder.Decode(config, in frame, output);

        Assert.AreEqual(250.0, output[0].Value, 1e-6); // clamped to HighRange
    }

    [TestMethod]
    public void Clamps_value_to_LowRange_after_conversion()
    {
        var decoder = new CanDecoder();
        // raw 0xCE = 206 -> formula: 206 - 256 = -50 °C (signed) -> -58 °F, clamped to LowRange -40 °F
        var ch = new CanChannelMapping(
            ChannelId: 12, Offset: 0, Length: 1, Mask: 0xFF,
            IsSigned: true, IsBigEndian: true,
            FormulaMultiplier: 1, FormulaDivider: 1, FormulaConst: 0);
        var msg = new CanMessageMapping(0, 0x202, Length: 1, IsBigEndian: true, Channels: [ch]);

        var def = new ChannelDefinition
        {
            DataType = "Temperature",
            BaseUnitType = "DegreeCelsius",
            OutputUnitType = "DegreeFahrenheit",
            LowRange = -40,
            HighRange = 300,
        };
        var converter = ChannelUnitConverter.Build(def, out _);

        var config = ActiveConfiguration.Empty with
        {
            Messages = new Dictionary<(int, uint), CanMessageMapping>
            {
                [(0, 0x202u)] = msg,
            },
            Channels = new Dictionary<int, ChannelDefinition> { [12] = def },
            UnitConverters = new Dictionary<int, ChannelUnitConverter> { [12] = converter },
        };

        var frame = new TimestampedFrame(0,
            new CanMessage { CanId = 0x202, Data = [0xCE], DataLength = 1 }, // -50 signed
            0, default);

        var output = new InternalChannelValue[1];
        _ = decoder.Decode(config, in frame, output);

        Assert.AreEqual(-40.0, output[0].Value, 1e-6); // clamped to LowRange
    }

    [TestMethod]
    public void Passthrough_when_no_converter_in_config()
    {
        var decoder = new CanDecoder();
        var ch = new CanChannelMapping(
            ChannelId: 20, Offset: 0, Length: 1, Mask: 0xFF,
            IsSigned: false, IsBigEndian: true,
            FormulaMultiplier: 1, FormulaDivider: 1, FormulaConst: 0);
        var msg = new CanMessageMapping(0, 0x300, Length: 1, IsBigEndian: true, Channels: [ch]);

        var config = ActiveConfiguration.Empty with
        {
            Messages = new Dictionary<(int, uint), CanMessageMapping>
            {
                [(0, 0x300u)] = msg,
            },
            Channels = new Dictionary<int, ChannelDefinition>
            {
                [20] = new ChannelDefinition { DataType = "Unitless" },
            },
            // No entry in UnitConverters for channel 20 — raw value passes through.
        };

        var frame = new TimestampedFrame(0,
            new CanMessage { CanId = 0x300, Data = [0x42], DataLength = 1 }, // 66 decimal
            0, default);

        var output = new InternalChannelValue[1];
        var n = decoder.Decode(config, in frame, output);

        Assert.AreEqual(1, n);
        Assert.AreEqual(66.0, output[0].Value, 1e-9);
    }
}
