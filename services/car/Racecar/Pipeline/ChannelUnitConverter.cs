using Channels;
using UnitsNet;

namespace Racecar.Pipeline;

/// <summary>
/// Pre-parsed unit conversion descriptor for a single channel.
/// Built once at configuration load time so that per-value conversion
/// on the hot pipeline path requires no string parsing.
/// </summary>
/// <remarks>
/// When <see cref="ChannelDefinition.DataType"/> is <c>Unitless</c> or
/// <c>String</c>, or when the base and output unit types are identical,
/// the raw value is returned unchanged (no UnitsNet allocation).
/// The converted value is then clamped to [<see cref="ChannelDefinition.LowRange"/>,
/// <see cref="ChannelDefinition.HighRange"/>] when a meaningful range is configured
/// (i.e. HighRange &gt; LowRange).
/// </remarks>
public sealed class ChannelUnitConverter
{
    private readonly Enum? _baseUnit;
    private readonly Enum? _outputUnit;
    private readonly bool _needsConversion;
    private readonly double _lowRange;
    private readonly double _highRange;
    private readonly bool _hasRange;

    private ChannelUnitConverter(
        Enum? baseUnit,
        Enum? outputUnit,
        bool needsConversion,
        double lowRange,
        double highRange)
    {
        _baseUnit = baseUnit;
        _outputUnit = outputUnit;
        _needsConversion = needsConversion;
        _lowRange = lowRange;
        _highRange = highRange;
        _hasRange = highRange > lowRange;
    }

    /// <summary>
    /// Converts <paramref name="baseValue"/> from the channel's base unit to its output
    /// unit and clamps the result to the configured range. Returns the value unchanged
    /// for Unitless and String channels or when unit parsing failed at build time.
    /// </summary>
    public double Convert(double baseValue)
    {
        double result = baseValue;

        if (_needsConversion && _baseUnit is not null && _outputUnit is not null)
        {
            var quantity = Quantity.From(baseValue, _baseUnit);
            result = quantity.ToUnit(_outputUnit).Value;
        }

        if (_hasRange)
        {
            result = Math.Clamp(result, _lowRange, _highRange);
        }

        return result;
    }

    /// <summary>
    /// Builds a <see cref="ChannelUnitConverter"/> for the given channel definition,
    /// parsing <see cref="ChannelDefinition.DataType"/>, <see cref="ChannelDefinition.BaseUnitType"/>,
    /// and <see cref="ChannelDefinition.OutputUnitType"/> into their UnitsNet enum equivalents.
    /// Returns a pass-through-only converter when parsing fails or conversion is not applicable.
    /// </summary>
    /// <param name="def">The channel definition to build a converter for.</param>
    /// <param name="warning">
    /// Set to a non-null diagnostic message when unit parsing failed and the converter
    /// will fall back to pass-through behaviour. Null when no issue was encountered.
    /// </param>
    public static ChannelUnitConverter Build(ChannelDefinition def, out string? warning)
    {
        warning = null;
        var lowRange = def.LowRange;
        var highRange = def.HighRange;

        // Unitless and String channels require no conversion.
        if (string.IsNullOrEmpty(def.DataType) ||
            def.DataType.Equals("Unitless", StringComparison.OrdinalIgnoreCase) ||
            def.DataType.Equals("String", StringComparison.OrdinalIgnoreCase))
        {
            return new ChannelUnitConverter(null, null, false, lowRange, highRange);
        }

        // No base unit: cannot convert, pass through.
        if (string.IsNullOrEmpty(def.BaseUnitType))
        {
            return new ChannelUnitConverter(null, null, false, lowRange, highRange);
        }

        if (!TryParseUnitEnum(def.DataType, def.BaseUnitType, out var baseUnit))
        {
            warning = $"Channel '{def.Name}': could not parse BaseUnitType '{def.BaseUnitType}' for DataType '{def.DataType}'. Unit conversion disabled.";
            return new ChannelUnitConverter(null, null, false, lowRange, highRange);
        }

        // No output unit or same as base: clamp only.
        if (string.IsNullOrEmpty(def.OutputUnitType) || def.OutputUnitType == def.BaseUnitType)
        {
            return new ChannelUnitConverter(baseUnit, baseUnit, false, lowRange, highRange);
        }

        if (!TryParseUnitEnum(def.DataType, def.OutputUnitType, out var outputUnit))
        {
            warning = $"Channel '{def.Name}': could not parse OutputUnitType '{def.OutputUnitType}' for DataType '{def.DataType}'. Unit conversion disabled.";
            return new ChannelUnitConverter(null, null, false, lowRange, highRange);
        }

        return new ChannelUnitConverter(baseUnit, outputUnit, true, lowRange, highRange);
    }

    /// <summary>
    /// Attempts to parse a unit enum value from its name using the supplied quantity type name.
    /// Only enum-name lookup is performed; abbreviation matching is intentionally omitted because
    /// channel definitions store UnitsNet enum names (e.g. <c>DegreeFahrenheit</c>), not abbreviations.
    /// </summary>
    private static bool TryParseUnitEnum(string dataType, string unitName, out Enum? unit)
    {
        unit = null;

        if (!Quantity.ByName.TryGetValue(dataType, out var quantityInfo))
            return false;

        if (!Enum.TryParse(quantityInfo.UnitType, unitName, ignoreCase: false, out var result))
            return false;

        unit = (Enum)result;
        return true;
    }
}
