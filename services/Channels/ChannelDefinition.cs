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
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the data type such as: "int", "float", "string", etc.
    /// </summary>
    public string DataType { get; set; } = string.Empty;
    public bool IsStringValue => DataType.Equals("string", StringComparison.OrdinalIgnoreCase);


    /// <summary>
    /// Gets or sets the base unit type for the channel, such as degrees, feet, etc. When the value is set, this is its units.
    /// </summary>
    public string BaseUnitType { get; set; } = string.Empty;
    public int BaseDecimalPlaces { get; set; }
    /// <summary>
    /// Gets or sets the type of unit used when the channel value is accessed such as for displaying values.
    /// </summary>
    public string OutputUnitType { get; set; } = string.Empty;
    public int OutputDecimalPlaces { get; set; }
}
