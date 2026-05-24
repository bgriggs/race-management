namespace Racecar.FuelAnalysis;

/// <summary>
/// In-car calibration window state. Opens on ECU Reset-Confirmed; closes at
/// the next <c>FuelFull</c> assertion while in the pit. Carries the snapshots
/// needed to derive the closing observation: opening throttle-integral and
/// opening fuel ground-truth value.
/// </summary>
/// <remarks>
/// Single-threaded by contract.
/// </remarks>
public sealed class CalibrationWindow
{
    private double _openTripFuel;
    private double _openIntegral;
    private long _openMonotonicTicks;

    public bool IsOpen { get; private set; }

    /// <summary>
    /// Open a calibration window. <paramref name="openTripFuel"/> is the
    /// ECU-reported trip fuel at the moment of Reset-Confirmed (expected to be
    /// at or near zero by definition of the reset rule).
    /// </summary>
    public void Open(double openTripFuel, double openIntegral, long monotonicTicks)
    {
        _openTripFuel = openTripFuel;
        _openIntegral = openIntegral;
        _openMonotonicTicks = monotonicTicks;
        IsOpen = true;
    }

    /// <summary>
    /// Close the window. <paramref name="closeTripFuel"/> is the ECU trip fuel
    /// at <c>FuelFull</c>+in-pit time (i.e., the amount the ECU has counted
    /// since the reset).
    /// </summary>
    /// <returns>
    /// Observation suitable for feeding <see cref="AlphaNGrid.ApplyWindowClose"/>
    /// and the scalar-k EMA, or <c>null</c> if the window is unusable
    /// (no fuel measured or zero integral).
    /// </returns>
    public CalibrationObservation? Close(double closeTripFuel, double closeIntegral, long monotonicTicks)
    {
        if (!IsOpen) return null;
        IsOpen = false;

        var deltaFuel = closeTripFuel - _openTripFuel;
        var deltaIntegral = closeIntegral - _openIntegral;
        var elapsed = TimeSpan.FromTicks(monotonicTicks - _openMonotonicTicks);

        if (deltaFuel <= 0 || deltaIntegral <= 0) return null;

        return new CalibrationObservation(
            DeltaFuelGallons: deltaFuel,
            DeltaIntegral: deltaIntegral,
            Elapsed: elapsed);
    }
}

/// <summary>
/// Result of a closed calibration window: total fuel burned over the window
/// (per the ground-truth source) and the throttle integral accumulated in the
/// same span. <c>k_observed = DeltaFuelGallons / DeltaIntegral</c>.
/// </summary>
public readonly record struct CalibrationObservation(
    double DeltaFuelGallons,
    double DeltaIntegral,
    TimeSpan Elapsed);
