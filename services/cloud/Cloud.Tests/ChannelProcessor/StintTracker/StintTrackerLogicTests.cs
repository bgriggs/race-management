using global::ChannelProcessor.StintTracker;

namespace Cloud.Tests.ChannelProcessor.StintTracker;

/// <summary>
/// Unit tests for the pure state-machine logic backing <see cref="StintTrackerWorker"/>.
/// Every branch of <see cref="StintTrackerLogic.ApplyInPit"/> is exercised plus the
/// heartbeat predicate and the emit-value computation. No Redis, no streams, no DI —
/// the logic is supposed to be pure and the tests prove it.
/// </summary>
[TestClass]
public class StintTrackerLogicTests
{
    private static readonly DateTime T0 = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    // ---- ApplyInPit: first-sight seeding ----

    [TestMethod]
    public void Apply_FirstSightOnTrack_AnchorsStintAtSampleTime_EmitsBaseline()
    {
        var state = new StintTrackerState();

        var r = StintTrackerLogic.ApplyInPit(state, sampleValue: false, T0);

        Assert.AreEqual(InPitDecision.SeedFirstSight, r.Decision);
        Assert.IsTrue(r.ShouldEmit);
        Assert.IsFalse(r.NewState.IsInPit!.Value);
        Assert.AreEqual(T0, r.NewState.StintStartedAtUtc);
        Assert.AreEqual(T0, r.NewState.LastInPitObservedAtUtc);
        Assert.AreEqual(0, r.NewState.StintCount);
    }

    [TestMethod]
    public void Apply_FirstSightInPit_LeavesStintAnchorNull_EmitsBaseline()
    {
        var state = new StintTrackerState();

        var r = StintTrackerLogic.ApplyInPit(state, sampleValue: true, T0);

        Assert.AreEqual(InPitDecision.SeedFirstSight, r.Decision);
        Assert.IsTrue(r.ShouldEmit);
        Assert.IsTrue(r.NewState.IsInPit!.Value);
        Assert.IsNull(r.NewState.StintStartedAtUtc);
    }

    // ---- ApplyInPit: pit-out edge (true → false) ----

    [TestMethod]
    public void Apply_PitOutEdge_AnchorsStintAtSample_StintCountUnchanged()
    {
        var state = State(isInPit: true, lastObs: T0, stintCount: 3);

        var r = StintTrackerLogic.ApplyInPit(state, sampleValue: false, T0.AddMinutes(2));

        Assert.AreEqual(InPitDecision.PitOut, r.Decision);
        Assert.IsTrue(r.ShouldEmit);
        Assert.IsFalse(r.NewState.IsInPit!.Value);
        Assert.AreEqual(T0.AddMinutes(2), r.NewState.StintStartedAtUtc);
        Assert.AreEqual(3, r.NewState.StintCount); // StintCount only increments on pit-IN
    }

    // ---- ApplyInPit: pit-in edge (false → true) ----

    [TestMethod]
    public void Apply_PitInEdge_ClearsAnchor_IncrementsStintCount()
    {
        var state = State(isInPit: false, lastObs: T0, stintStartedAt: T0, stintCount: 2);

        var r = StintTrackerLogic.ApplyInPit(state, sampleValue: true, T0.AddMinutes(30));

        Assert.AreEqual(InPitDecision.PitIn, r.Decision);
        Assert.IsTrue(r.ShouldEmit);
        Assert.IsTrue(r.NewState.IsInPit!.Value);
        Assert.IsNull(r.NewState.StintStartedAtUtc);
        Assert.AreEqual(3, r.NewState.StintCount);
    }

    // ---- ApplyInPit: no edge ----

    [TestMethod]
    public void Apply_SameValueNoEdge_ReturnsOriginalState_NoEmit()
    {
        var state = State(isInPit: false, lastObs: T0, stintStartedAt: T0, stintCount: 5);

        var r = StintTrackerLogic.ApplyInPit(state, sampleValue: false, T0.AddMinutes(1));

        Assert.AreEqual(InPitDecision.NoEdge, r.Decision);
        Assert.IsFalse(r.ShouldEmit);
        Assert.AreSame(state, r.NewState); // unchanged
    }

    // ---- ApplyInPit: out-of-order ----

    [TestMethod]
    public void Apply_OutOfOrderSample_AtSameTimestamp_Ignored()
    {
        var state = State(isInPit: true, lastObs: T0.AddMinutes(5), stintCount: 1);

        var r = StintTrackerLogic.ApplyInPit(state, sampleValue: false, T0.AddMinutes(5));

        Assert.AreEqual(InPitDecision.OutOfOrder, r.Decision);
        Assert.IsFalse(r.ShouldEmit);
        Assert.AreSame(state, r.NewState);
    }

    [TestMethod]
    public void Apply_OutOfOrderSample_EarlierThanLastObserved_Ignored()
    {
        var state = State(isInPit: true, lastObs: T0.AddMinutes(10), stintCount: 1);

        var r = StintTrackerLogic.ApplyInPit(state, sampleValue: false, T0.AddMinutes(2));

        Assert.AreEqual(InPitDecision.OutOfOrder, r.Decision);
        Assert.IsFalse(r.ShouldEmit);
    }

    // ---- ApplyInPit: purity ----

    [TestMethod]
    public void Apply_DoesNotMutateInputState()
    {
        var state = State(isInPit: false, lastObs: T0, stintStartedAt: T0, stintCount: 1);

        StintTrackerLogic.ApplyInPit(state, sampleValue: true, T0.AddMinutes(20));

        // Caller's state must still reflect pre-call values; mutating the input would
        // create subtle bugs in the worker which reads-then-emits-with-old-state in some
        // paths.
        Assert.IsFalse(state.IsInPit!.Value);
        Assert.AreEqual(T0, state.LastInPitObservedAtUtc);
        Assert.AreEqual(T0, state.StintStartedAtUtc);
        Assert.AreEqual(1, state.StintCount);
    }

    // ---- ApplyInPit: monotonic stint count progression ----

    [TestMethod]
    public void Apply_MultiplePitCycles_StintCountMonotonicallyIncreases()
    {
        var state = new StintTrackerState();

        // First sight on-track: stint #1 starts.
        state = StintTrackerLogic.ApplyInPit(state, false, T0).NewState;
        // Pit-in: count → 1.
        state = StintTrackerLogic.ApplyInPit(state, true, T0.AddMinutes(30)).NewState;
        // Pit-out: stint #2 starts.
        state = StintTrackerLogic.ApplyInPit(state, false, T0.AddMinutes(35)).NewState;
        // Pit-in: count → 2.
        state = StintTrackerLogic.ApplyInPit(state, true, T0.AddMinutes(60)).NewState;
        // Pit-out: stint #3 starts.
        state = StintTrackerLogic.ApplyInPit(state, false, T0.AddMinutes(65)).NewState;

        Assert.AreEqual(2, state.StintCount);
        Assert.AreEqual(T0.AddMinutes(65), state.StintStartedAtUtc);
    }

    // ---- ShouldHeartbeat ----

    [TestMethod]
    public void Heartbeat_OnTrack_DueForEmit_ReturnsTrue()
    {
        var state = State(isInPit: false, lastObs: T0, stintStartedAt: T0, lastEmittedAt: T0);

        Assert.IsTrue(StintTrackerLogic.ShouldHeartbeat(state, T0.AddSeconds(75), TimeSpan.FromSeconds(60)));
    }

    [TestMethod]
    public void Heartbeat_OnTrack_NotYetDue_ReturnsFalse()
    {
        var state = State(isInPit: false, lastObs: T0, stintStartedAt: T0, lastEmittedAt: T0);

        Assert.IsFalse(StintTrackerLogic.ShouldHeartbeat(state, T0.AddSeconds(30), TimeSpan.FromSeconds(60)));
    }

    [TestMethod]
    public void Heartbeat_InPit_AlwaysFalse()
    {
        var state = State(isInPit: true, lastObs: T0, lastEmittedAt: T0);

        Assert.IsFalse(StintTrackerLogic.ShouldHeartbeat(state, T0.AddSeconds(120), TimeSpan.FromSeconds(60)));
    }

    [TestMethod]
    public void Heartbeat_UnknownPitState_ReturnsFalse()
    {
        var state = new StintTrackerState { IsInPit = null };

        Assert.IsFalse(StintTrackerLogic.ShouldHeartbeat(state, T0.AddHours(1), TimeSpan.FromSeconds(60)));
    }

    [TestMethod]
    public void Heartbeat_OnTrackButNoStintAnchor_ReturnsFalse()
    {
        var state = State(isInPit: false, lastObs: T0, stintStartedAt: null, lastEmittedAt: T0);

        Assert.IsFalse(StintTrackerLogic.ShouldHeartbeat(state, T0.AddSeconds(120), TimeSpan.FromSeconds(60)));
    }

    [TestMethod]
    public void Heartbeat_NeverEmittedBefore_ReturnsTrue()
    {
        var state = State(isInPit: false, lastObs: T0, stintStartedAt: T0, lastEmittedAt: null);

        Assert.IsTrue(StintTrackerLogic.ShouldHeartbeat(state, T0.AddSeconds(30), TimeSpan.FromSeconds(60)));
    }

    // ---- ComputeEmittedValues ----

    [TestMethod]
    public void Compute_OnTrack_MinutesIsElapsedSinceStintStart()
    {
        var state = State(isInPit: false, stintStartedAt: T0, stintCount: 4);

        var (minutes, count) = StintTrackerLogic.ComputeEmittedValues(state, T0.AddMinutes(22.5));

        Assert.AreEqual(22.5, minutes, 1e-9);
        Assert.AreEqual(4, count);
    }

    [TestMethod]
    public void Compute_InPit_MinutesIsZero()
    {
        var state = State(isInPit: true, stintStartedAt: null, stintCount: 2);

        var (minutes, count) = StintTrackerLogic.ComputeEmittedValues(state, T0.AddMinutes(15));

        Assert.AreEqual(0.0, minutes);
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public void Compute_NoStintAnchor_MinutesIsZero()
    {
        var state = State(isInPit: false, stintStartedAt: null, stintCount: 1);

        var (minutes, _) = StintTrackerLogic.ComputeEmittedValues(state, T0.AddMinutes(99));

        Assert.AreEqual(0.0, minutes);
    }

    [TestMethod]
    public void Compute_ClockSkewWhereNowIsBeforeStintStart_MinutesFloorsToZero()
    {
        // Defensive: if a heartbeat tick sees an out-of-skew `now` that predates the stint
        // anchor, we'd publish a negative duration. The Math.Max in the implementation
        // floors at zero.
        var state = State(isInPit: false, stintStartedAt: T0.AddMinutes(10));

        var (minutes, _) = StintTrackerLogic.ComputeEmittedValues(state, T0);

        Assert.AreEqual(0.0, minutes);
    }

    // ---- helpers ----

    private static StintTrackerState State(
        bool? isInPit = null,
        DateTime? lastObs = null,
        DateTime? stintStartedAt = null,
        int stintCount = 0,
        DateTime? lastEmittedAt = null) => new()
    {
        IsInPit = isInPit,
        LastInPitObservedAtUtc = lastObs,
        StintStartedAtUtc = stintStartedAt,
        StintCount = stintCount,
        LastEmittedAtUtc = lastEmittedAt,
    };
}
