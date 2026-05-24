using ChannelProcessor.FuelAnalysis.ChannelInput;
using ChannelProcessor.FuelAnalysis.State;

namespace ChannelProcessor.FuelAnalysis.Pace;

/// <summary>
/// Per-message updater for the lap-time rolling window and session lap history that feed
/// <see cref="DriverPaceCalculator"/>. Detects a new lap via a value-edge on
/// <c>LastLapTime</c>, then excludes in-laps and out-laps from the baseline using the
/// <see cref="CarFuelState.InPitTransitionedSinceLastLap"/> flag maintained by
/// <see cref="Windows.StintLifecycle"/>.
/// <para>
/// <b>Assumption:</b> <c>LastLapTime</c> arrives as a numeric string in <b>seconds</b>
/// (e.g., "83.456"). If a future source publishes it in <c>mm:ss.fff</c> format
/// <see cref="Channels.ChannelValue.GetValueDouble"/> returns 0 and the value falls below
/// <see cref="MinValidLapTimeSeconds"/>, so it's silently skipped — pace stays at 1.0.
/// </para>
/// </summary>
public sealed class DriverPaceTracker
{
    public const double MinValidLapTimeSeconds = 30.0;
    public const double MaxValidLapTimeSeconds = 600.0;
    private const int RecentWindowSize = 5;
    private const int SessionMaxRetained = 200;

    public CarFuelState Apply(CarFuelState state, FuelInputs inputs)
    {
        if (inputs.LastLapTimeSeconds is not TimestampedDouble lap) return state;
        if (state.LastLapTimeTimestamp is DateTime last && lap.TimestampUtc <= last) return state;

        var newValue = lap.Value;
        state.LastLapTimeTimestamp = lap.TimestampUtc;

        if (newValue < MinValidLapTimeSeconds || newValue > MaxValidLapTimeSeconds)
        {
            // Unparseable or implausible — don't update LastLapTimeSeconds (keep last good baseline).
            return state;
        }

        // A repeat of the same value means the channel re-emitted but no new lap completed.
        var isNewLap = state.LastLapTimeSeconds is null
            || Math.Abs(newValue - state.LastLapTimeSeconds.Value) > 0.0001;
        state.LastLapTimeSeconds = newValue;
        if (!isNewLap) return state;

        var isInPitNow = inputs.InPit?.Value ?? state.LastInPitValue;
        var wasInPitDuringLap = state.InPitTransitionedSinceLastLap;
        state.InPitTransitionedSinceLastLap = false;

        if (isInPitNow || wasInPitDuringLap)
        {
            // In-lap (entering pit) or out-lap (just left pit) — excluded from baseline per design.md §798.
            return state;
        }

        // TODO: also exclude laps run under non-green RaceFlagState once that's wired
        // (RaceFlagState is currently stubbed Green per the user's slice-1 direction).

        state.RecentLapTimes.Add(newValue);
        if (state.RecentLapTimes.Count > RecentWindowSize)
            state.RecentLapTimes.RemoveAt(0);

        state.SessionLapTimes.Add(newValue);
        if (state.SessionLapTimes.Count > SessionMaxRetained)
            state.SessionLapTimes.RemoveAt(0);

        return state;
    }
}
