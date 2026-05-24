namespace Cloud.Shared.FuelAnalysis;

/// <summary>
/// Reads/writes the latest <see cref="FuelRangeSnapshot"/> per car in Redis under
/// <see cref="Consts.FUEL_SNAPSHOT_KEY"/>. Written by ChannelProcessor's reconciler on
/// every emission tick; read by WebApi's <c>FuelController</c> to serve the Race Monitor
/// detail panel.
/// </summary>
public interface IFuelSnapshotStore
{
    Task SetAsync(string carKey, FuelRangeSnapshot snapshot, CancellationToken ct = default);
    Task<FuelRangeSnapshot?> GetAsync(string carKey, CancellationToken ct = default);
    Task ClearAsync(string carKey, CancellationToken ct = default);
}
