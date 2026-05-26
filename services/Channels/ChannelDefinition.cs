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

    [DefaultValueInRange]
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

    /// <summary>
    /// Where the channel's values are produced and which tiers they are transmitted to.
    /// Defaults to <see cref="ChannelDistribution.CarToCloud"/>, matching today's behavior
    /// for car-side telemetry.
    /// </summary>
    public ChannelDistribution Distribution { get; set; } = ChannelDistribution.CarToCloud;

    /// <summary>
    /// When true, <see cref="Distribution"/> is pinned to the template value and cannot be
    /// changed in the Edit Channel UI or via the configuration save endpoint. Used for
    /// reserved channels whose owning feature genuinely requires a specific routing
    /// (e.g., the ThrottleProxy* outputs that the cloud reconciler estimators depend on).
    /// </summary>
    public bool IsDistributionLocked { get; set; }

    /// <summary>
    /// What entity the channel's values are bound to. Defaults to <see cref="ChannelScope.PerCar"/>.
    /// </summary>
    public ChannelScope Scope { get; set; } = ChannelScope.PerCar;

    /// <summary>
    /// Identifier of the feature that owns this channel's lifecycle (e.g., "fuel-analysis",
    /// "throttle-consumption"). When non-null on a reserved-channel template, the channel
    /// is auto-injected into a car configuration when its feature is enabled, hidden from
    /// the user's reserved-channel picker, and removed when the feature is disabled. The
    /// value propagates to the per-car channel instance at injection time, where the UI
    /// uses it to lock editing and deletion.
    /// </summary>
    [StringLength(32)]
    public string? ManagedByFeature { get; set; }

    /// <summary>
    /// Identifier of the internal feature whose code writes values to this channel (e.g.,
    /// "fuel-analysis" for the FuelReconciler's range outputs, "throttle-consumption" for
    /// the in-car ThrottleProxyConsumer's outputs). When non-null, the channel-usage UI
    /// counts the channel as "used" and labels it "Output: {feature}" — analogous to a
    /// math/timer/counter definition declaring its output channel. Distinct from
    /// <see cref="ManagedByFeature"/>, which is lifecycle ownership: a channel can be
    /// managed by a feature (auto-injected with the toggle) without being produced by it
    /// (e.g., ThrottlePosition is consumed by throttle-consumption but produced by the
    /// user's CAN-bus mapping).
    /// </summary>
    [StringLength(32)]
    public string? ProducedByFeature { get; set; }
}

[AttributeUsage(AttributeTargets.Property)]
file sealed class DefaultValueInRangeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not double defaultValue) return ValidationResult.Success;

        var instance = context.ObjectInstance;
        var type = instance.GetType();
        var low = (double)(type.GetProperty(nameof(ChannelDefinition.LowRange))?.GetValue(instance) ?? double.MinValue);
        var high = (double)(type.GetProperty(nameof(ChannelDefinition.HighRange))?.GetValue(instance) ?? double.MaxValue);

        return defaultValue >= low && defaultValue <= high
            ? ValidationResult.Success
            : new ValidationResult($"DefaultValue must be between LowRange ({low}) and HighRange ({high}) inclusive.", [context.MemberName!]);
    }
}
