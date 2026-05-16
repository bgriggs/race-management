using MessagePack;
using UnitsNet;

namespace Channels;

[MessagePackObject]
public class ChannelValue
{
    /// <summary>
    /// Compact session index negotiated at connection time for high-frequency streaming.
    /// This index will correspond to the Car's configuration Channel list index for the channel value being used.
    /// </summary>
    [Key(0)]
    public ushort SessionIndex { get; set; }

    [Key(1)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Wall-clock UTC timestamp this value was produced. Set by the transmit
    /// boundary in the Racecar pipeline; remains <see cref="DateTime.MinValue"/>
    /// for values constructed elsewhere unless explicitly populated.
    /// </summary>
    [Key(2)]
    public DateTime Timestamp { get; set; }

    public int GetValueInt()
    {
        return int.TryParse(Value, out var result) ? result : 0;
    }

    public double GetValueDouble()
    {
        return double.TryParse(Value, out var result) ? result : 0;
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
