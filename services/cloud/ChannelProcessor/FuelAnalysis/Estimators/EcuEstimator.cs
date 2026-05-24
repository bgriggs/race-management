using Cloud.Shared.Database.Models.FuelAnalysis;

namespace ChannelProcessor.FuelAnalysis.Estimators;

/// <summary>
/// ECU Estimator — derives fuel remaining from the <c>TripFuel</c> reserved channel.
/// Available only when the current FuelWindow's ECU reset is at least Reset-Inferred
/// AND <c>TripFuel</c> has updated within the last 10 s (design.md §624–636).
/// </summary>
public sealed class EcuEstimator : IFuelEstimator
{
    public string Name => "ecu";
    public bool BypassesRateModel => false;

    private static readonly TimeSpan StaleWatchdog = TimeSpan.FromSeconds(10);
    private const double SigmaConfirmedGallons = 0.5;
    private const double SigmaInferredGallons = 1.0;
    // Don't trust a derived consumption rate over too-short a window; fall back to seed.
    private static readonly TimeSpan MinWindowForRate = TimeSpan.FromSeconds(60);

    public EstimatorReading Compute(in EstimatorContext context)
    {
        var state = context.State;
        if (state.CurrentWindowEcuResetState == EcuResetState.Unreset)
            return Unavailable("ECU not reset since last refuel");
        if (state.CurrentWindowEcuResetState == EcuResetState.Unknown)
            return Unavailable("ECU reset state not yet classified");

        if (state.LastTripFuelValue is not double tripFuel || state.LastTripFuelTimestamp is not DateTime tripTs)
            return Unavailable("no TripFuel reading");

        if ((context.NowUtc - tripTs) > StaleWatchdog)
            return Unavailable("TripFuel stale (>10s)");

        var tankCap = context.FuelConfig.TankCapacityGallons;
        if (tankCap <= 0)
            return Unavailable("TankCapacityGallons not configured");

        var fuelRemaining = Math.Max(0, tankCap - tripFuel);

        // Observed rate over current window
        double? baseRate = null;
        if (state.OpenFuelWindowOpenedAt is DateTime openedAt)
        {
            var elapsed = context.NowUtc - openedAt;
            if (elapsed >= MinWindowForRate && tripFuel > 0)
            {
                baseRate = tripFuel / elapsed.TotalMinutes;
            }
        }
        baseRate ??= context.FuelConfig.DefaultConsumptionGalPerMin;

        var sigma = state.CurrentWindowEcuResetState == EcuResetState.ResetConfirmed
            ? SigmaConfirmedGallons
            : SigmaInferredGallons;

        return new EstimatorReading(true, null, fuelRemaining, sigma, baseRate);
    }

    private static EstimatorReading Unavailable(string reason) =>
        new(false, reason, null, null, null);
}
