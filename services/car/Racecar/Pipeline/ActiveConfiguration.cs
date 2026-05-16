using Channels;

namespace Racecar.Pipeline;

/// <summary>
/// Per-channel mapping from a CAN frame slice to a numeric base value.
/// </summary>
public sealed record CanChannelMapping(
    int ChannelId,
    int Offset,
    int Length,
    ulong Mask,
    bool IsSigned,
    bool IsBigEndian,
    double FormulaMultiplier,
    double FormulaDivider,
    double FormulaConst);

/// <summary>
/// All decode information for a single CAN message ID on a single bus.
/// </summary>
public sealed record CanMessageMapping(
    int CanBusIndex,
    uint CanId,
    int Length,
    bool IsBigEndian,
    IReadOnlyList<CanChannelMapping> Channels);

/// <summary>
/// Per-channel deadband used by the change filter.
/// </summary>
public sealed record ChannelChangeRule(int ChannelId, double Deadband);

/// <summary>
/// Immutable snapshot of all per-config state used by the pipeline. Swapped
/// atomically on configuration reload via <see cref="Interlocked.Exchange{T}(ref T, T)"/>.
/// </summary>
public sealed record ActiveConfiguration
{
    public required Guid ConfigurationId { get; init; }

    /// <summary>Compact pipeline channel ID (session index) → channel definition.</summary>
    public required IReadOnlyDictionary<int, ChannelDefinition> Channels { get; init; }

    /// <summary>Channel definition Guid → compact pipeline channel ID.</summary>
    public required IReadOnlyDictionary<Guid, int> ChannelIdsByDefinition { get; init; }

    /// <summary>(canBusIndex, canId) → message mapping with channel slices.</summary>
    public required IReadOnlyDictionary<(int Bus, uint Id), CanMessageMapping> Messages { get; init; }

    public required IReadOnlyDictionary<int, double> Deadbands { get; init; }

    /// <summary>Per-channel definition hash; used to migrate state across reloads.</summary>
    public required IReadOnlyDictionary<int, ulong> DefinitionHashes { get; init; }

    public required IDerivedChannelEvaluator DerivedEvaluator { get; init; }

    /// <summary>
    /// Pre-parsed unit converters keyed by compact channel ID. Built once at
    /// configuration load time; used by <see cref="CanDecoder"/> on every CAN frame.
    /// </summary>
    public required IReadOnlyDictionary<int, ChannelUnitConverter> UnitConverters { get; init; }

    /// <summary>Default deadband for an unmapped channel (strict equality).</summary>
    public double GetDeadband(int channelId) =>
        Deadbands.TryGetValue(channelId, out var d) ? d : 0d;

    public static ActiveConfiguration Empty { get; } = new()
    {
        ConfigurationId = Guid.Empty,
        Channels = new Dictionary<int, ChannelDefinition>(),
        ChannelIdsByDefinition = new Dictionary<Guid, int>(),
        Messages = new Dictionary<(int, uint), CanMessageMapping>(),
        Deadbands = new Dictionary<int, double>(),
        DefinitionHashes = new Dictionary<int, ulong>(),
        DerivedEvaluator = NoOpDerivedEvaluator.Instance,
        UnitConverters = new Dictionary<int, ChannelUnitConverter>(),
    };
}
