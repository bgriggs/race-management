using Common.FuelAnalysis;

namespace ChannelProcessor.FuelAnalysis.Config;

/// <summary>
/// Reads the per-car <see cref="CarFuelConfig"/> from the currently-active
/// <c>CarConfiguration</c> JSON. The user opted to keep fuel parameters embedded in the
/// config JSON rather than splitting them into a dedicated Postgres table (slice 1
/// decision); this reader fronts that JSON with a HybridCache so the per-message hot
/// path does not re-parse on every tick.
/// </summary>
public interface ICarFuelConfigReader
{
    Task<CarFuelConfig?> GetAsync(string carKey, CancellationToken ct = default);
}
