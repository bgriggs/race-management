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
            new CanMessage { CanId = 0x100, Data = new byte[] { 0, 0 }, DataLength = 2 },
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
            Channels: new[] { ch });
        var config = ConfigFor(msg);

        // raw = 0x0190 = 400 -> 400 * 0.1 - 40 = 0
        var frame = new TimestampedFrame(0,
            new CanMessage
            {
                CanId = 0x123,
                Data = new byte[] { 0x01, 0x90, 0, 0, 0, 0, 0, 0 },
                DataLength = 8,
            },
            MonotonicTicks: 1234, WallTime: new DateTime(2026, 5, 10));

        var output = new InternalChannelValue[4];
        var n = decoder.Decode(config, in frame, output);

        Assert.AreEqual(1, n);
        Assert.AreEqual(1, output[0].ChannelId);
        Assert.AreEqual(0.0, output[0].BaseValue, 1e-9);
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
        var config = ConfigFor(new CanMessageMapping(0, 0x1, 1, true, new[] { ch }));

        // 0xFF signed -> -1
        var frame = new TimestampedFrame(0,
            new CanMessage { CanId = 1, Data = new byte[] { 0xFF }, DataLength = 1 },
            0, default);
        var output = new InternalChannelValue[1];
        _ = decoder.Decode(config, in frame, output);
        Assert.AreEqual(-1.0, output[0].BaseValue);
    }
}
