using Cloud.Shared.Database.Models.FuelAnalysis;

namespace ChannelProcessor.FuelAnalysis.Calibration;

/// <summary>
/// Reads the most recent <see cref="CalibrationFactor"/> for a car. Backed by
/// HybridCache with a short TTL so engineer-driven overrides from WebApi land in the
/// reconciler within seconds, while keeping the per-snapshot lookup off the DB hot path.
/// Returns <c>null</c> when no factor has ever been recorded for the car.
/// </summary>
public interface ICalibrationFactorReader
{
    Task<CalibrationFactor?> GetLatestAsync(int teamId, string carNumber, CancellationToken ct = default);

    /// <summary>Invalidate the cache for this car (called by <see cref="CalibrationFactorLearner"/> immediately after a write so the same worker pass sees the fresh row).</summary>
    Task InvalidateAsync(int teamId, string carNumber, CancellationToken ct = default);
}
