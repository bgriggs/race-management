namespace Racecar.FuelAnalysis;

/// <summary>
/// ECU reset state. The in-car module only acts on
/// <see cref="ResetConfirmed"/> — the cloud reconciler handles the wider
/// <c>ResetInferred</c> / <c>Unreset</c> distinctions for the ECU Estimator.
/// </summary>
public enum EcuResetState
{
    /// <summary>No anchor seen yet — neutral starting state.</summary>
    Unknown,

    /// <summary><c>FuelFull</c> asserted AND <c>TripFuel</c> dropped below threshold within the window.</summary>
    ResetConfirmed,

    /// <summary>Anchor passed without a corresponding <c>TripFuel</c> drop. ECU Estimator unavailable for this window.</summary>
    Unreset,
}

/// <summary>
/// Local ECU reset detector for the in-car calibration loop. Watches
/// <c>FuelFull</c> assertions and <c>TripFuel</c> drops within a configured
/// post-anchor window and emits a reset-state classification.
/// </summary>
/// <remarks>
/// Narrower than the cloud reconciler's full Refuel Event detection: the in-car
/// module only needs to know when a calibration window may open (Reset-Confirmed
/// — design.md "A calibration window opens when ECU enters Reset-Confirmed
/// state"). Single-threaded by contract.
/// </remarks>
public sealed class EcuResetDetector
{
    private readonly TimeSpan _resetWindow;
    private readonly double _resetThresholdGallons;

    private long? _fuelFullAssertedTicks;
    private double? _lastTripFuel;
    private EcuResetState _currentState = EcuResetState.Unknown;

    public EcuResetDetector(FuelAnalysisOptions options)
    {
        _resetWindow = options.EcuResetWindow;
        _resetThresholdGallons = options.EcuResetTripFuelThresholdGallons;
    }

    /// <summary>Most recent classification.</summary>
    public EcuResetState CurrentState => _currentState;

    /// <summary>
    /// <c>True</c> on the channel update that produced the latest transition into
    /// <see cref="EcuResetState.ResetConfirmed"/>. Read-and-clear semantics: the
    /// orchestrator opens a calibration window on this edge.
    /// </summary>
    public bool ResetJustDetected { get; private set; }

    /// <summary>Called when a fresh <c>FuelFull</c> value arrives (raw value treated as boolean).</summary>
    public void OnFuelFull(double fuelFullValue, long monotonicTicks)
    {
        ResetJustDetected = false;
        if (fuelFullValue > 0.5)
        {
            _fuelFullAssertedTicks = monotonicTicks;
            EvaluateAnchor(monotonicTicks);
        }
    }

    /// <summary>Called when a fresh <c>TripFuel</c> value arrives (gal).</summary>
    public void OnTripFuel(double tripFuelGallons, long monotonicTicks)
    {
        ResetJustDetected = false;
        _lastTripFuel = tripFuelGallons;
        if (_fuelFullAssertedTicks is not null)
        {
            EvaluateAnchor(monotonicTicks);
        }
    }

    /// <summary>
    /// Periodically advance the detector — used by the consumer's emit loop to
    /// close out an open anchor once the reset window has elapsed without
    /// satisfying the Reset-Confirmed rule.
    /// </summary>
    public void Tick(long monotonicTicks)
    {
        ResetJustDetected = false;
        if (_fuelFullAssertedTicks is not long assertedTicks) return;
        var elapsed = TimeSpan.FromTicks(monotonicTicks - assertedTicks);
        if (elapsed < _resetWindow) return;

        if (_currentState != EcuResetState.ResetConfirmed)
        {
            _currentState = EcuResetState.Unreset;
        }
        _fuelFullAssertedTicks = null;
    }

    private void EvaluateAnchor(long monotonicTicks)
    {
        if (_fuelFullAssertedTicks is not long assertedTicks) return;
        if (_lastTripFuel is not double tripFuel) return;
        var elapsed = TimeSpan.FromTicks(monotonicTicks - assertedTicks);
        if (elapsed > _resetWindow)
        {
            if (_currentState != EcuResetState.ResetConfirmed)
            {
                _currentState = EcuResetState.Unreset;
            }
            _fuelFullAssertedTicks = null;
            return;
        }

        if (tripFuel < _resetThresholdGallons)
        {
            var previous = _currentState;
            _currentState = EcuResetState.ResetConfirmed;
            ResetJustDetected = previous != EcuResetState.ResetConfirmed;
            _fuelFullAssertedTicks = null;
        }
    }
}
