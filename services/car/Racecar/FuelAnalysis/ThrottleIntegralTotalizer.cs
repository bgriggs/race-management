namespace Racecar.FuelAnalysis;

/// <summary>
/// Attribution returned by <see cref="ThrottleIntegralTotalizer.OnSample"/>.
/// Carries the values the cell-grid should record for the elapsed interval
/// using left-rectangle integration (previous-sample TPS/RPM held over Δt).
/// </summary>
public readonly record struct SampleAttribution(
    bool HasInterval,
    double PrevTps,
    double PrevRpm,
    double DeltaSeconds);

/// <summary>
/// Time-weighted running sum of throttle position: <c>Σ TPSᵢ × Δtᵢ</c>, units
/// of (TPS-percent · seconds). Driven by monotonic-clock timestamps so the
/// integral is immune to wall-clock adjustments.
///
/// Engine-off rule: once <c>EngineRPM = 0</c> has been sustained for the
/// configured <see cref="FuelAnalysisOptions.EngineOffResetAfter"/> duration,
/// the integral is reset to zero on the next sample.
/// </summary>
/// <remarks>
/// Single-threaded by contract — only the throttle proxy consumer's mailbox
/// pump calls it.
/// </remarks>
public sealed class ThrottleIntegralTotalizer
{
    private const double MaxIntervalSeconds = 10.0;

    private readonly TimeSpan _engineOffResetAfter;

    private double _integral;
    private long? _lastSampleTicks;
    private double _lastTps;
    private double _lastRpm;
    private long? _engineStoppedAtTicks;

    public ThrottleIntegralTotalizer(FuelAnalysisOptions options)
    {
        _engineOffResetAfter = options.EngineOffResetAfter;
    }

    /// <summary>Current integral in TPS-percent · seconds.</summary>
    public double Integral => _integral;

    /// <summary>Number of samples ingested since last reset (diagnostic).</summary>
    public long SampleCount { get; private set; }

    /// <summary>
    /// Advance with a new (TPS, RPM) sample at <paramref name="monotonicTicks"/>.
    /// Returns the previous-sample attribution suitable for grid recording.
    /// </summary>
    public SampleAttribution OnSample(double tps, double rpm, long monotonicTicks)
    {
        SampleAttribution attribution = default;

        if (_lastSampleTicks is long prev)
        {
            var dt = TimeSpan.FromTicks(monotonicTicks - prev).TotalSeconds;
            if (dt > 0 && dt < MaxIntervalSeconds)
            {
                _integral += _lastTps * dt;
                attribution = new SampleAttribution(true, _lastTps, _lastRpm, dt);
            }

            CheckEngineOffReset(rpm, monotonicTicks);
        }
        else
        {
            _engineStoppedAtTicks = rpm <= 0 ? monotonicTicks : null;
        }

        _lastSampleTicks = monotonicTicks;
        _lastTps = tps;
        _lastRpm = rpm;
        SampleCount++;
        return attribution;
    }

    /// <summary>Explicit reset.</summary>
    public void Reset()
    {
        _integral = 0;
        _lastSampleTicks = null;
        _lastTps = 0;
        _lastRpm = 0;
        _engineStoppedAtTicks = null;
        SampleCount = 0;
    }

    private void CheckEngineOffReset(double rpm, long monotonicTicks)
    {
        if (rpm > 0)
        {
            _engineStoppedAtTicks = null;
            return;
        }

        _engineStoppedAtTicks ??= monotonicTicks;
        var stoppedFor = TimeSpan.FromTicks(monotonicTicks - _engineStoppedAtTicks.Value);
        if (stoppedFor >= _engineOffResetAfter)
        {
            _integral = 0;
            SampleCount = 0;
        }
    }
}
