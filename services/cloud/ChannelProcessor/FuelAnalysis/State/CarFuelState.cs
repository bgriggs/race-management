using MessagePack;

namespace ChannelProcessor.FuelAnalysis.State;

/// <summary>
/// Per-car runtime state for the Fuel Reconciler, persisted to Redis under
/// <see cref="Cloud.Shared.Consts.FUEL_STATE_KEY"/>. Rebuildable from Postgres on startup
/// (Slice 2 rehydration: query the at-most-one open <c>FuelWindow</c> for the car and
/// reconstruct refuel/stint pointers; the edge-detector fields default to empty and
/// repopulate from incoming channel values).
/// <para>
/// Serialized with MessagePack <c>[MessagePackObject(true)]</c> (string-keyed) for
/// schema-evolution forgiveness — adding fields in later slices does not break in-flight
/// state records.
/// </para>
/// </summary>
[MessagePackObject(true)]
public sealed class CarFuelState
{
    /// <summary>The race this state is scoped to. If the active race changes, the state is reset.</summary>
    public int RaceId { get; set; }

    public int? OpenFuelWindowId { get; set; }
    public int? OpenFuelWindowStartRefuelEventId { get; set; }
    public DateTime? OpenFuelWindowOpenedAt { get; set; }

    public int? OpenStintId { get; set; }
    /// <summary>Start of the most recent stint — the currently-open one if any, otherwise the most recently closed. Used by the FuelFull anchor's "stint age ≥ 15 min" guard.</summary>
    public DateTime? MostRecentStintStartedAt { get; set; }

    // --- Latest observed channel values, for edge detection ---

    public bool LastFuelFullValue { get; set; }
    public DateTime? LastFuelFullTimestamp { get; set; }

    public double? LastFuelLevelValue { get; set; }
    public DateTime? LastFuelLevelTimestamp { get; set; }

    public bool LastInPitValue { get; set; }
    public DateTime? LastInPitTimestamp { get; set; }

    public double? LastGpsSpeedMph { get; set; }
    public DateTime? LastGpsSpeedTimestamp { get; set; }

    public double? LastTripFuelValue { get; set; }
    public DateTime? LastTripFuelTimestamp { get; set; }

    public double? LastFuelUsedValue { get; set; }
    public DateTime? LastFuelUsedTimestamp { get; set; }

    // --- In-car ThrottleProxy outputs (CarToCloud channels) ---

    public double? LastThrottleProxyFuelUsedValue { get; set; }
    public DateTime? LastThrottleProxyFuelUsedTimestamp { get; set; }
    public double? LastThrottleProxyRateValue { get; set; }
    public DateTime? LastThrottleProxyRateTimestamp { get; set; }
    /// <summary>0..100 — min of in-car k-convergence score and current-cell sample coverage.</summary>
    public double? LastThrottleProxyConfidenceValue { get; set; }
    public DateTime? LastThrottleProxyConfidenceTimestamp { get; set; }
    /// <summary>0..100 — % of alpha-N grid cells with sufficient calibration samples.</summary>
    public double? LastThrottleProxyGridCoverageValue { get; set; }
    public DateTime? LastThrottleProxyGridCoverageTimestamp { get; set; }
    /// <summary>Cloud-maintained running ∫ ThrottleProxyRate dt (gallons) — the grid estimator's totalizer. Null when no rate samples have been observed yet (incl. immediately after a pod-restart rehydrate).</summary>
    public double? CloudIntegratedFuelUsedGallons { get; set; }

    // --- FuelLevel-rise anchor tracker ---

    /// <summary>When a sustained FuelLevel rise began while stationary. Null when no rise is in progress.</summary>
    public DateTime? FuelLevelRiseStartedAt { get; set; }
    /// <summary>FuelLevel observed at the start of the rise — anchors the &gt;1 gal delta check.</summary>
    public double? FuelLevelAtRiseStart { get; set; }

    // --- ECU reset classification tracker for the currently-open FuelWindow ---

    public DateTime? CurrentWindowFuelFullAssertedAt { get; set; }
    public DateTime? CurrentWindowTripFuelDroppedAt { get; set; }
    /// <summary>UTC instant after which the ECU reset classifier should commit a verdict — opened-at + 60s, or pit-out, whichever came first.</summary>
    public DateTime? CurrentWindowResetClassifyDeadline { get; set; }
    /// <summary>True once the EcuResetState for the current window has been written back to Postgres — prevents repeated updates.</summary>
    public bool CurrentWindowResetClassified { get; set; }

    // --- Cached open-FuelWindow context (mirrors fields from the start RefuelEvent so
    //     estimators do not need to round-trip to Postgres on every snapshot tick) ---

    /// <summary>Current window's start-RefuelEvent <c>EcuResetState</c>. Refreshed by <see cref="Refuel.EcuResetClassifier"/> on verdict.</summary>
    public Cloud.Shared.Database.Models.FuelAnalysis.EcuResetState CurrentWindowEcuResetState { get; set; }
    /// <summary>Manual fuel volume entered for the window (the start-RefuelEvent's <c>EnteredFuelGallons</c>). Null while not yet entered.</summary>
    public double? CurrentWindowEnteredFuelGallons { get; set; }
    /// <summary>FuelUsed reading captured at the moment the window opened — baseline for the FlowMeter estimator's per-window arithmetic.</summary>
    public double? CurrentWindowFlowMeterFuelUsedAtOpen { get; set; }
    /// <summary>In-car ThrottleProxyFuelUsed at window open — baseline for <c>throttle.integral</c>.</summary>
    public double? CurrentWindowThrottleProxyFuelUsedAtOpen { get; set; }
    /// <summary>Cloud-integrated ∫ ThrottleProxyRate dt at window open — baseline for <c>throttle.grid</c>. Null when the integral wasn't initialized yet at window open (rehydrate-from-DB case).</summary>
    public double? CurrentWindowCloudIntegratedFuelUsedAtOpen { get; set; }

    // --- Snapshot emission ---

    public DateTime? LastSnapshotEmittedAt { get; set; }
    /// <summary>Set true by event triggers (new Refuel Event, estimator availability change) to force the next tick to emit immediately rather than wait for the 1-min cadence.</summary>
    public bool ForceNextSnapshot { get; set; }

    // --- Driver-pace tracker (rolling lap-time window + session baseline) ---

    /// <summary>Last seen <c>LastLapTime</c> value (seconds) — used to detect a NEW lap when the channel re-emits.</summary>
    public double? LastLapTimeSeconds { get; set; }
    public DateTime? LastLapTimeTimestamp { get; set; }
    /// <summary>Rolling window of the most recent N valid lap times (in/out laps excluded). Mean of these is <c>recentAvgLapTime</c>.</summary>
    public List<double> RecentLapTimes { get; set; } = [];
    /// <summary>All valid lap times seen this session (capped FIFO). Median of these is <c>sessionBaselineLapTime</c> once enough laps have accumulated.</summary>
    public List<double> SessionLapTimes { get; set; } = [];
    /// <summary>Set true whenever <c>InPit</c> edges (either direction) since the last lap completion. Cleared each time a lap is processed; if true at lap completion, the lap is treated as in/out and skipped.</summary>
    public bool InPitTransitionedSinceLastLap { get; set; }

    // --- Outlier debounce, keyed by estimator name ---

    public Dictionary<string, OutlierDebounceEntry> OutlierDebounce { get; set; } = new();

    /// <summary>Snapshot of estimator availability at the last tick. Used to detect availability changes that should force an immediate emit.</summary>
    public Dictionary<string, bool> LastEstimatorAvailability { get; set; } = new();
}

[MessagePack.MessagePackObject(true)]
public sealed class OutlierDebounceEntry
{
    /// <summary>The currently committed outlier state for this estimator.</summary>
    public bool IsOutlier { get; set; }
    /// <summary>UTC instant at which the raw outlier classification first started disagreeing with <see cref="IsOutlier"/>; null when in agreement.</summary>
    public DateTime? PendingFlipSince { get; set; }
}
