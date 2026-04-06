using System.Text;
using UnitsNet;

namespace Channels;

public class ChannelValue
{
    /// <summary>
    /// ID of the channel as specified with a channel definition.
    /// </summary>
    public int Id { get; set; }

    public string Value { get; set; } = string.Empty;

    public int GetValueInt()
    {
        return int.TryParse(Value, out var result) ? result : 0;
    }

    public double GetValueDouble()
    {
        return double.TryParse(Value, out var result) ? result : 0;
    }

    public void SetBaseValue(double value, ChannelDefinition definition)
    {
        var zeros = GetZeros(definition.BaseDecimalPlaces);
        Value = value.ToString("0." + zeros);
    }

    private static string GetZeros(int count)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            _ = sb.Append('0');
        }
        return sb.ToString();
    }

    public IQuantity? GetOutputQuantity(ChannelDefinition definition)
    {
        if (!double.TryParse(Value, out double v))
            return null;

        if (string.IsNullOrEmpty(definition.BaseUnitType))
            return null;

        // Create the quantity expressed in its stored base unit.
        IQuantity baseQuantity = Quantity.FromUnitAbbreviation(v, definition.BaseUnitType);

        // If no output unit is configured, or it matches the base unit, return as-is.
        if (string.IsNullOrEmpty(definition.OutputUnitType) || definition.OutputUnitType == definition.BaseUnitType)
            return baseQuantity;

        // Resolve the target unit enum from its abbreviation and convert.
        IQuantity targetReference = Quantity.FromUnitAbbreviation(0, definition.OutputUnitType);
        return baseQuantity.ToUnit(targetReference.Unit);
    }
}
