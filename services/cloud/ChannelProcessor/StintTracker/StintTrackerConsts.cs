namespace ChannelProcessor.StintTracker;

internal static class StintTrackerConsts
{
    /// <summary>Consumer group on the <c>car-channel-values</c> stream. Independent of the
    /// fuel-analysis and alarm groups so this worker sees every message without contention.</summary>
    public const string CONSUMER_GROUP = "channelproc-stint";

    /// <summary>Per-car stint state. MessagePack-serialized <see cref="StintTrackerState"/>.</summary>
    public const string STATE_KEY = "stint-state:{0}:{1}"; // teamId, carNumber

    /// <summary>Pattern used by the timer worker's SCAN to enumerate active state rows.</summary>
    public const string STATE_KEY_SCAN_PATTERN = "stint-state:*";

    /// <summary>Redis TTL on per-car state; renewed on every write. Long enough to span an
    /// enduro plus inter-session gaps; short enough to garbage-collect abandoned states.</summary>
    public static readonly TimeSpan StateTtl = TimeSpan.FromHours(48);

    /// <summary>Minimum interval between heartbeat <c>CurrentStintMinutes</c> emissions for a
    /// car on track. ADR-0008 calls for 60-second emissions; the timer loop runs more
    /// frequently and checks this lower bound.</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(60);
}
