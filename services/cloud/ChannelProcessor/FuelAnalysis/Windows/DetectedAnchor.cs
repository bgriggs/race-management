using Cloud.Shared.Database.Models.FuelAnalysis;

namespace ChannelProcessor.FuelAnalysis.Windows;

/// <summary>
/// A single anchor detection emitted by <see cref="Refuel.RefuelEventDetector"/>. The
/// orchestrator passes it to <see cref="FuelWindowLifecycle"/> which decides whether it
/// merges into an existing Refuel Event (within the 2-min anchor-agreement window) or
/// creates a new one and opens a fresh <see cref="FuelWindow"/>.
/// </summary>
public readonly record struct DetectedAnchor(
    RefuelAnchor Anchor,
    DateTime AtUtc,
    bool InPitOrSlowAtAssertion);
