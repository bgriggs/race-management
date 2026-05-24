namespace ChannelProcessor.FuelAnalysis.Estimators;

/// <summary>
/// PitFill Estimator — the always-on baseline. Depends only on the manually-entered
/// fuel-jug volume at the current FuelWindow's start and elapsed wall-clock time, so it
/// survives telemetry disconnects (design.md §649–657). Returns a wider sigma than
/// telemetry-backed estimators to reflect the ±1 gal jug-reading noise.
/// </summary>
public sealed class PitFillEstimator : IFuelEstimator
{
    public string Name => "pitfill";
    public bool BypassesRateModel => false;

    /// <summary>Wide when the jug volume has not yet been entered (estimator relies on configured seed only).</summary>
    private const double SigmaNoEntryGallons = 3.0;
    /// <summary>Tighter once a real jug volume is captured.</summary>
    private const double SigmaEnteredGallons = 1.5;

    public EstimatorReading Compute(in EstimatorContext context)
    {
        var state = context.State;
        var tankCap = context.FuelConfig.TankCapacityGallons;
        if (tankCap <= 0)
            return Unavailable("TankCapacityGallons not configured");

        if (state.OpenFuelWindowOpenedAt is not DateTime openedAt)
            return Unavailable("no open FuelWindow");

        var elapsedMinutes = Math.Max(0, (context.NowUtc - openedAt).TotalMinutes);
        // Slice 3: use the configured DefaultConsumptionGalPerMin as baseRate.
        // EMA refinement across closed FuelWindows lands in a follow-up slice.
        var baseRate = context.FuelConfig.DefaultConsumptionGalPerMin;
        if (baseRate <= 0)
            return Unavailable("DefaultConsumptionGalPerMin not configured");

        var enteredGallons = state.CurrentWindowEnteredFuelGallons;
        var startingFuel = enteredGallons ?? tankCap; // best guess when not entered
        var consumedSoFar = baseRate * elapsedMinutes;
        var fuelRemaining = Math.Max(0, startingFuel - consumedSoFar);

        var sigma = enteredGallons is null ? SigmaNoEntryGallons : SigmaEnteredGallons;

        return new EstimatorReading(true, null, fuelRemaining, sigma, baseRate);
    }

    private static EstimatorReading Unavailable(string reason) =>
        new(false, reason, null, null, null);
}
