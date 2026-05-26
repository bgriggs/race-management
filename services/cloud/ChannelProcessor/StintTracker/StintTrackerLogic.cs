namespace ChannelProcessor.StintTracker;

/// <summary>
/// Pure state-machine logic for stint tracking. Extracted from
/// <see cref="StintTrackerWorker"/> so the decision rules can be unit-tested without
/// Redis or the channel-publish pipeline. The worker remains responsible for I/O:
/// reading state, writing state, and invoking the publisher with the values this
/// computes.
///
/// Contract: every function in this class is pure — given the same inputs it produces
/// the same outputs and never mutates the input <see cref="StintTrackerState"/>.
/// </summary>
public static class StintTrackerLogic
{
    /// <summary>
    /// Applies an inbound <c>InPit</c> sample to the current state. Returns the new
    /// state, whether the caller should emit/persist, and which branch the decision
    /// took (for logging and tests).
    /// </summary>
    public static InPitApplyResult ApplyInPit(StintTrackerState state, bool sampleValue, DateTime sampleTimestampUtc)
    {
        // Out-of-order samples are ignored so state stays monotonic by observation time.
        if (state.LastInPitObservedAtUtc is DateTime last && sampleTimestampUtc <= last)
            return new InPitApplyResult(state, ShouldEmit: false, InPitDecision.OutOfOrder);

        var next = Clone(state);
        next.IsInPit = sampleValue;
        next.LastInPitObservedAtUtc = sampleTimestampUtc;

        var prev = state.IsInPit;

        if (prev is null)
        {
            // First sight of this car. Seed: anchor the stint start at the same instant
            // when the car is currently on track. The first emission gives consumers a
            // baseline value to read; precision tightens on the next pit cycle.
            if (!sampleValue)
                next.StintStartedAtUtc = sampleTimestampUtc;
            return new InPitApplyResult(next, ShouldEmit: true, InPitDecision.SeedFirstSight);
        }

        if (prev == sampleValue)
        {
            // No edge — state is unchanged, no emission.
            return new InPitApplyResult(state, ShouldEmit: false, InPitDecision.NoEdge);
        }

        if (sampleValue)
        {
            // Pit-in. Close the current stint and bump the count.
            next.StintStartedAtUtc = null;
            next.StintCount += 1;
            return new InPitApplyResult(next, ShouldEmit: true, InPitDecision.PitIn);
        }

        // Pit-out. Start a new stint at the sample timestamp.
        next.StintStartedAtUtc = sampleTimestampUtc;
        return new InPitApplyResult(next, ShouldEmit: true, InPitDecision.PitOut);
    }

    /// <summary>
    /// Determines whether the periodic heartbeat loop should emit
    /// <c>CurrentStintMinutes</c> for a car in this state. The car must be (i) known
    /// to be on track, (ii) have a stint start anchor, and (iii) be due for emission
    /// per the configured interval.
    /// </summary>
    public static bool ShouldHeartbeat(StintTrackerState state, DateTime nowUtc, TimeSpan heartbeatInterval)
    {
        if (state.IsInPit is not false) return false; // null (unknown) or true (in pit)
        if (state.StintStartedAtUtc is null) return false;
        if (state.LastEmittedAtUtc is DateTime last && (nowUtc - last) < heartbeatInterval) return false;
        return true;
    }

    /// <summary>
    /// Computes the emit-time values for <c>CurrentStintMinutes</c> and
    /// <c>StintCount</c>. <c>CurrentStintMinutes</c> is the elapsed wall-time since
    /// the stint start, floored at zero; emits <c>0</c> when the car is in the pit
    /// or the stint anchor is unknown.
    /// </summary>
    public static (double CurrentStintMinutes, int StintCount) ComputeEmittedValues(StintTrackerState state, DateTime nowUtc)
    {
        var minutes = state is { IsInPit: false, StintStartedAtUtc: DateTime started }
            ? Math.Max(0.0, (nowUtc - started).TotalMinutes)
            : 0.0;
        return (minutes, state.StintCount);
    }

    private static StintTrackerState Clone(StintTrackerState s) => new()
    {
        IsInPit = s.IsInPit,
        LastInPitObservedAtUtc = s.LastInPitObservedAtUtc,
        StintStartedAtUtc = s.StintStartedAtUtc,
        StintCount = s.StintCount,
        LastEmittedAtUtc = s.LastEmittedAtUtc,
    };
}

public enum InPitDecision
{
    /// <summary>Sample timestamp is at-or-before the last observed timestamp; ignored.</summary>
    OutOfOrder,
    /// <summary>First-ever sample for this car; state was seeded and a baseline emit is due.</summary>
    SeedFirstSight,
    /// <summary>Sample value equals the prior value; no state change, no emit.</summary>
    NoEdge,
    /// <summary>InPit flipped <c>false → true</c>; closed the active stint and incremented <c>StintCount</c>.</summary>
    PitIn,
    /// <summary>InPit flipped <c>true → false</c>; opened a new stint at the sample timestamp.</summary>
    PitOut,
}

/// <summary>
/// Outcome of <see cref="StintTrackerLogic.ApplyInPit"/>: the new state plus whether
/// the caller should emit channels and persist.
/// </summary>
public sealed record InPitApplyResult(StintTrackerState NewState, bool ShouldEmit, InPitDecision Decision);
