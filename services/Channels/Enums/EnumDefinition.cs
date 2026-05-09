using System.ComponentModel.DataAnnotations;

namespace Channels.Enums;

/// <summary>
/// Mapping from a raw channel value to a display string. This allows for channels to be displayed as discrete values instead of just numbers. 
/// For example, a channel that outputs 0 or 1 could have an enum definition that maps 0 to "Off" and 1 to "On".
/// </summary>
public class EnumDefinition
{
    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the enum such as "Pump State". Maximum length is 20 characters.
    /// </summary>
    [MaxLength(20)]
    public required string Name { get; set; }

    /// <summary>
    /// Mapping of raw channel values to display strings. The Source property is the raw value as a string, and the Value property is the integer 
    /// value that the channel outputs. For example, for a channel that outputs 0 or 1, you could have an EnumValueDefinition with Source = "Off" 
    /// and Value = 0, and another EnumValueDefinition with Source = "On" and Value = 1.
    /// </summary>
    public List<EnumValueDefinition> Values { get; set; } = [];
}

/// <summary>
/// Represents a single value in an enumeration, including its source name and integer value.
/// </summary>
/// <param name="Source">The name or identifier of the enumeration value.</param>
/// <param name="Value">The integer value associated with the enumeration entry.</param>
public record EnumValueDefinition(string Source, int Value);