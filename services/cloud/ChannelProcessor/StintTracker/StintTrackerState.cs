using MessagePack;

namespace ChannelProcessor.StintTracker;

/// <summary>
/// Per-car stint state held in Redis. Reset to defaults when no prior state exists (first
/// sight of a car). Mutates on every <c>InPit</c> transition and on every heartbeat emission.
/// </summary>
[MessagePackObject]
public sealed class StintTrackerState
{
    /// <summary>Last observed <c>InPit</c> value (null until the first message).</summary>
    [Key(0)] public bool? IsInPit { get; set; }

    /// <summary>Timestamp of the most recent <c>InPit</c> sample we've processed.</summary>
    [Key(1)] public DateTime? LastInPitObservedAtUtc { get; set; }

    /// <summary>UTC when the current stint started (i.e., last pit-out edge, or first sight
    /// while the car was on track). Null while the car is in the pit.</summary>
    [Key(2)] public DateTime? StintStartedAtUtc { get; set; }

    /// <summary>Number of completed pit-in events observed for this car since state was
    /// initialized. The next stint will be number <c>StintCount + 1</c>.</summary>
    [Key(3)] public int StintCount { get; set; }

    /// <summary>UTC of the most recent <c>CurrentStintMinutes</c> emission. Used by the
    /// heartbeat timer to throttle to one emit per <see cref="StintTrackerConsts.HeartbeatInterval"/>.</summary>
    [Key(4)] public DateTime? LastEmittedAtUtc { get; set; }
}
