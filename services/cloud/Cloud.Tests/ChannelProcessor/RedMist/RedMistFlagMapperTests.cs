using global::ChannelProcessor.RedMist;
using RedMist.TimingCommon.Models;

namespace Cloud.Tests.ChannelProcessor.RedMist;

/// <summary>
/// Locks in the RedMist <c>Flags</c> → <c>RaceFlagState</c> string mapping. The mapping is
/// part of an external integration contract — the Fuel Reconciler's flag multipliers are
/// fed by these exact strings (design.md §784). Any drift here silently breaks fuel range
/// estimates, so each branch is asserted explicitly.
/// </summary>
[TestClass]
public class RedMistFlagMapperTests
{
    [TestMethod]
    [DataRow(Flags.Green,     "Green",  DisplayName = "Green pass-through")]
    [DataRow(Flags.Yellow,    "Yellow", DisplayName = "Yellow pass-through")]
    [DataRow(Flags.Red,       "Red",    DisplayName = "Red pass-through")]
    [DataRow(Flags.Purple35,  "Code35", DisplayName = "Purple35 → Code35")]
    [DataRow(Flags.Purple60,  "Code60", DisplayName = "Purple60 → Code60")]
    public void Map_KnownFlags_ProducesExpectedString(Flags input, string expected)
    {
        Assert.AreEqual(expected, RedMistFlagMapper.Map(input));
    }

    [TestMethod]
    [DataRow(Flags.White,     DisplayName = "White → Green (final lap is racing pace)")]
    [DataRow(Flags.Checkered, DisplayName = "Checkered → Green (session ending, no slowdown to model)")]
    [DataRow(Flags.Unknown,   DisplayName = "Unknown → Green (matches default-when-absent rule in design.md §792)")]
    public void Map_ConservativeFallbacksThatRouteToGreen(Flags input)
    {
        Assert.AreEqual("Green", RedMistFlagMapper.Map(input));
    }

    [TestMethod]
    public void Map_Black_RoutesToYellow_Conservatively()
    {
        // Black is a stop-go penalty: not a session-wide flag, but treating it as Yellow is
        // conservative — better to over-estimate flag-multiplier savings than to keep
        // computing at full Green consumption while the car is in the box.
        Assert.AreEqual("Yellow", RedMistFlagMapper.Map(Flags.Black));
    }

    [TestMethod]
    public void Map_UnknownEnumValue_ReturnsNull()
    {
        // Defensive: a new flag added upstream that we haven't classified yet must surface
        // as null so the publisher skips it rather than guessing.
        var bogus = (Flags)int.MaxValue;
        Assert.IsNull(RedMistFlagMapper.Map(bogus));
    }

    [TestMethod]
    public void Map_CoversEveryDefinedFlagsValue()
    {
        // Forcing coverage: a new Flags enum member that lacks a mapping returns null and
        // gets dropped silently. This test fails on the day someone adds a new flag so the
        // table above gets updated deliberately.
        foreach (Flags flag in Enum.GetValues<Flags>())
        {
            var mapped = RedMistFlagMapper.Map(flag);
            Assert.IsNotNull(mapped, $"Flags.{flag} is unmapped — add it to RedMistFlagMapper.Map().");
        }
    }
}
