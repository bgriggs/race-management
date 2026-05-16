using System.ComponentModel.DataAnnotations;

namespace Channels;

/// <summary>
/// Channel metadata definition, which defines the properties of a channel, such as its name, data type, and units.
/// </summary>
public class ChannelDefinition
{
    /// <summary>
    /// Globally unique identifier for this channel, stable across all tiers (car, cloud, local config)
    /// without requiring a central authority. Generated once at definition time and never changed.
    /// </summary>
    public Guid Id { get; set; }
    public bool IsReserved { get; set; }

    [StringLength(16)]
    public string Category { get; set; } = string.Empty;
    
    [StringLength(25)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(6)]
    public string Abbreviation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the data type such as: Temperature, Length, Volume, VolumeFlow, Duration, Speed, Pressure, Force, Voltage, Mass, Ratio, Current, Resistance.
    /// Special types: Unitless, String
    /// </summary>
    public string DataType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the base unit type for the channel, such as degrees, feet, etc. When the value is set, this is its units.
    /// </summary>
    public string BaseUnitType { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the type of unit used when the channel value is accessed such as for displaying values.
    /// </summary>
    public string OutputUnitType { get; set; } = string.Empty;
    public int OutputDecimalPlaces { get; set; }

    public double LowRange { get; set; }
    public double HighRange { get; set; }
    public double DefaultValue { get; set; }

    [StringLength(16)]
    public string GroupTag { get; set; } = string.Empty;

    /// <summary>
    /// Optional conversion from the value to specified string enum values, stored as a reference to a separate enum conversion definition. 
    /// This allows for mapping numeric values to human-readable strings, such as mapping 0, 1, 2 to "Off", "On", "Auto" for a channel that
    /// represents a mode setting. The enum conversion definition would define the mapping of numeric values to string values for the channel.
    /// </summary>
    public Guid? EnumConversion { get; set; }

    /// <summary>
    /// Amount of time in milliseconds between updates from the channel source before considering the value timed out and set to default.
    /// </summary>
    public int TimeoutMs { get; set; } = 3000;
}
