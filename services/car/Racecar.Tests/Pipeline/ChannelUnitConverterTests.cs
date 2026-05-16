using Channels;
using Racecar.Pipeline;

namespace Racecar.Tests.Pipeline;

[TestClass]
public sealed class ChannelUnitConverterTests
{
    // ── Build: pass-through cases ─────────────────────────────────────────────

    [TestMethod]
    public void Build_Unitless_returns_passthrough_no_warning()
    {
        var def = new ChannelDefinition { DataType = "Unitless", LowRange = 0, HighRange = 0 };
        var converter = ChannelUnitConverter.Build(def, out var warning);

        Assert.IsNull(warning);
        Assert.AreEqual(42.0, converter.Convert(42.0), 1e-9);
    }

    [TestMethod]
    public void Build_String_returns_passthrough_no_warning()
    {
        var def = new ChannelDefinition { DataType = "String", LowRange = 0, HighRange = 0 };
        var converter = ChannelUnitConverter.Build(def, out var warning);

        Assert.IsNull(warning);
        Assert.AreEqual(7.0, converter.Convert(7.0), 1e-9);
    }

    [TestMethod]
    public void Build_empty_DataType_returns_passthrough_no_warning()
    {
        var def = new ChannelDefinition { DataType = string.Empty, LowRange = 0, HighRange = 0 };
        var converter = ChannelUnitConverter.Build(def, out var warning);

        Assert.IsNull(warning);
        Assert.AreEqual(5.0, converter.Convert(5.0), 1e-9);
    }

    [TestMethod]
    public void Build_empty_BaseUnitType_returns_passthrough_no_warning()
    {
        var def = new ChannelDefinition
        {
            DataType = "Temperature",
            BaseUnitType = string.Empty,
            OutputUnitType = "DegreeFahrenheit",
            LowRange = 0,
            HighRange = 0,
        };
        var converter = ChannelUnitConverter.Build(def, out var warning);

        Assert.IsNull(warning);
        Assert.AreEqual(25.0, converter.Convert(25.0), 1e-9);
    }

    [TestMethod]
    public void Build_same_base_and_output_unit_returns_clamponly_no_warning()
    {
        var def = new ChannelDefinition
        {
            DataType = "Temperature",
            BaseUnitType = "DegreeCelsius",
            OutputUnitType = "DegreeCelsius",
            LowRange = -40,
            HighRange = 150,
        };
        var converter = ChannelUnitConverter.Build(def, out var warning);

        Assert.IsNull(warning);
        // No conversion; value returned unchanged (within range)
        Assert.AreEqual(100.0, converter.Convert(100.0), 1e-9);
    }

    // ── Build: warning cases ──────────────────────────────────────────────────

    [TestMethod]
    public void Build_invalid_BaseUnitType_emits_warning_and_passthrough()
    {
        var def = new ChannelDefinition
        {
            Name = "TestChannel",
            DataType = "Temperature",
            BaseUnitType = "NotAUnit",
            OutputUnitType = "DegreeFahrenheit",
            LowRange = 0,
            HighRange = 0,
        };
        var converter = ChannelUnitConverter.Build(def, out var warning);

        Assert.IsNotNull(warning);
        StringAssert.Contains(warning, "NotAUnit");
        StringAssert.Contains(warning, "TestChannel");
        Assert.AreEqual(25.0, converter.Convert(25.0), 1e-9);
    }

    [TestMethod]
    public void Build_invalid_OutputUnitType_emits_warning_and_passthrough()
    {
        var def = new ChannelDefinition
        {
            Name = "TestChannel",
            DataType = "Temperature",
            BaseUnitType = "DegreeCelsius",
            OutputUnitType = "NotAUnit",
            LowRange = 0,
            HighRange = 0,
        };
        var converter = ChannelUnitConverter.Build(def, out var warning);

        Assert.IsNotNull(warning);
        StringAssert.Contains(warning, "NotAUnit");
        Assert.AreEqual(25.0, converter.Convert(25.0), 1e-9);
    }

    [TestMethod]
    public void Build_unknown_DataType_emits_warning_and_passthrough()
    {
        var def = new ChannelDefinition
        {
            Name = "TestChannel",
            DataType = "MadeUpType",
            BaseUnitType = "DegreeCelsius",
            OutputUnitType = "DegreeFahrenheit",
            LowRange = 0,
            HighRange = 0,
        };
        var converter = ChannelUnitConverter.Build(def, out var warning);

        Assert.IsNotNull(warning);
        StringAssert.Contains(warning, "DegreeCelsius");
        Assert.AreEqual(25.0, converter.Convert(25.0), 1e-9);
    }

    // ── Convert: unit conversion ──────────────────────────────────────────────

    [TestMethod]
    public void Convert_Celsius_to_Fahrenheit_freezing_point()
    {
        var converter = BuildTemperatureConverter(lowRange: -100, highRange: 300);

        // 0 °C → 32 °F
        Assert.AreEqual(32.0, converter.Convert(0.0), 1e-6);
    }

    [TestMethod]
    public void Convert_Celsius_to_Fahrenheit_boiling_point()
    {
        var converter = BuildTemperatureConverter(lowRange: -100, highRange: 300);

        // 100 °C → 212 °F
        Assert.AreEqual(212.0, converter.Convert(100.0), 1e-6);
    }

    [TestMethod]
    public void Convert_Celsius_to_Fahrenheit_negative_value()
    {
        var converter = BuildTemperatureConverter(lowRange: -100, highRange: 300);

        // -40 °C → -40 °F (the crossover point)
        Assert.AreEqual(-40.0, converter.Convert(-40.0), 1e-6);
    }

    // ── Convert: range clamping ───────────────────────────────────────────────

    [TestMethod]
    public void Convert_clamps_below_LowRange()
    {
        // Unitless channel with range only
        var def = new ChannelDefinition
        {
            DataType = "Unitless",
            LowRange = 0,
            HighRange = 100,
        };
        var converter = ChannelUnitConverter.Build(def, out _);

        Assert.AreEqual(0.0, converter.Convert(-10.0), 1e-9);
    }

    [TestMethod]
    public void Convert_clamps_above_HighRange()
    {
        var def = new ChannelDefinition
        {
            DataType = "Unitless",
            LowRange = 0,
            HighRange = 100,
        };
        var converter = ChannelUnitConverter.Build(def, out _);

        Assert.AreEqual(100.0, converter.Convert(150.0), 1e-9);
    }

    [TestMethod]
    public void Convert_does_not_clamp_when_value_within_range()
    {
        var def = new ChannelDefinition
        {
            DataType = "Unitless",
            LowRange = 0,
            HighRange = 100,
        };
        var converter = ChannelUnitConverter.Build(def, out _);

        Assert.AreEqual(50.0, converter.Convert(50.0), 1e-9);
    }

    [TestMethod]
    public void Convert_inclusive_at_LowRange_boundary()
    {
        var def = new ChannelDefinition
        {
            DataType = "Unitless",
            LowRange = 10,
            HighRange = 90,
        };
        var converter = ChannelUnitConverter.Build(def, out _);

        Assert.AreEqual(10.0, converter.Convert(10.0), 1e-9);
    }

    [TestMethod]
    public void Convert_inclusive_at_HighRange_boundary()
    {
        var def = new ChannelDefinition
        {
            DataType = "Unitless",
            LowRange = 10,
            HighRange = 90,
        };
        var converter = ChannelUnitConverter.Build(def, out _);

        Assert.AreEqual(90.0, converter.Convert(90.0), 1e-9);
    }

    [TestMethod]
    public void Convert_no_clamp_when_LowRange_equals_HighRange()
    {
        // When LowRange == HighRange (default 0 == 0), no clamping should occur.
        var def = new ChannelDefinition
        {
            DataType = "Unitless",
            LowRange = 0,
            HighRange = 0,
        };
        var converter = ChannelUnitConverter.Build(def, out _);

        Assert.AreEqual(999.0, converter.Convert(999.0), 1e-9);
        Assert.AreEqual(-999.0, converter.Convert(-999.0), 1e-9);
    }

    [TestMethod]
    public void Convert_clamps_after_unit_conversion()
    {
        // 150 °C converts to 302 °F, which should be clamped to HighRange of 250 °F.
        var converter = BuildTemperatureConverter(lowRange: -100, highRange: 250);

        Assert.AreEqual(250.0, converter.Convert(150.0), 1e-6);
    }

    [TestMethod]
    public void Convert_clamps_below_after_unit_conversion()
    {
        // -50 °C converts to -58 °F, which should be clamped to LowRange of -40 °F.
        var converter = BuildTemperatureConverter(lowRange: -40, highRange: 300);

        Assert.AreEqual(-40.0, converter.Convert(-50.0), 1e-6);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ChannelUnitConverter BuildTemperatureConverter(double lowRange, double highRange)
    {
        var def = new ChannelDefinition
        {
            DataType = "Temperature",
            BaseUnitType = "DegreeCelsius",
            OutputUnitType = "DegreeFahrenheit",
            LowRange = lowRange,
            HighRange = highRange,
        };
        var converter = ChannelUnitConverter.Build(def, out var warning);
        Assert.IsNull(warning, $"Unexpected build warning: {warning}");
        return converter;
    }
}
