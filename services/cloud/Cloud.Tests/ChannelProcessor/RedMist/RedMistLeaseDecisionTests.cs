using global::ChannelProcessor.RedMist;

namespace Cloud.Tests.ChannelProcessor.RedMist;

/// <summary>
/// Decision-table tests for the per-tick lease verdict. The decision table is small
/// (6 outputs over 4 distinct input shapes plus the held vs. not-held axis), so this
/// covers every cell.
///
///   currentlyHeld | candidate              | heldEvent  | action
///   ------------- | ---------------------- | ---------- | -----------------------
///   false         | null                   | n/a        | PublishNoEventPaired
///   false         | InWindow=false         | n/a        | PublishNoEventPaired
///   false         | InWindow=true (id 100) | n/a        | TryAcquire
///   true          | null                   | any        | DetachNoCandidate
///   true          | InWindow=false         | any        | DetachOutOfWindow
///   true          | InWindow=true (id 100) | 100        | Renew
///   true          | InWindow=true (id 100) | 200        | DetachEventChanged
/// </summary>
[TestClass]
public class RedMistLeaseDecisionTests
{
    private static readonly ActivationCandidate InWindowCandidate = new(
        RaceId: 1,
        RaceName: "Race",
        RedMistEventId: 100,
        RedMistOrganizationId: null,
        RedMistAccessCode: null,
        StartUtc: new DateTime(2026, 6, 1, 15, 0, 0, DateTimeKind.Utc),
        EndUtc: new DateTime(2026, 6, 1, 17, 0, 0, DateTimeKind.Utc),
        InWindow: true);

    private static readonly ActivationCandidate OutOfWindowCandidate = InWindowCandidate with { InWindow = false };

    // ---- Not currently held ----

    [TestMethod]
    public void NotHeld_NullCandidate_PublishesNoEventPaired()
    {
        var action = RedMistLeaseDecision.Decide(currentlyHeld: false, heldEventId: null, candidate: null);
        Assert.AreEqual(LeaseAction.PublishNoEventPaired, action);
    }

    [TestMethod]
    public void NotHeld_CandidateOutOfWindow_PublishesNoEventPaired()
    {
        // Out-of-window race exists for the team but the activation rule has it as inactive
        // — same as "no event paired" from the worker's perspective. No SETNX attempt.
        var action = RedMistLeaseDecision.Decide(currentlyHeld: false, heldEventId: null, OutOfWindowCandidate);
        Assert.AreEqual(LeaseAction.PublishNoEventPaired, action);
    }

    [TestMethod]
    public void NotHeld_CandidateInWindow_TriesAcquire()
    {
        var action = RedMistLeaseDecision.Decide(currentlyHeld: false, heldEventId: null, InWindowCandidate);
        Assert.AreEqual(LeaseAction.TryAcquire, action);
    }

    // ---- Currently held ----

    [TestMethod]
    public void Held_NullCandidate_DetachesAsNoCandidate()
    {
        var action = RedMistLeaseDecision.Decide(currentlyHeld: true, heldEventId: 100, candidate: null);
        Assert.AreEqual(LeaseAction.DetachNoCandidate, action);
    }

    [TestMethod]
    public void Held_CandidateOutOfWindow_DetachesAsOutOfWindow()
    {
        // A race that ended (past 30-min post-pad). The detach reason matters for logs
        // and status; the worker maps "out of window" to a specific detail string.
        var action = RedMistLeaseDecision.Decide(currentlyHeld: true, heldEventId: 100, OutOfWindowCandidate);
        Assert.AreEqual(LeaseAction.DetachOutOfWindow, action);
    }

    [TestMethod]
    public void Held_CandidateMatchesHeldEvent_Renews()
    {
        var action = RedMistLeaseDecision.Decide(currentlyHeld: true, heldEventId: 100, InWindowCandidate);
        Assert.AreEqual(LeaseAction.Renew, action);
    }

    [TestMethod]
    public void Held_CandidateIsDifferentEvent_DetachesAsEventChanged()
    {
        // The activation rule picks event 100 right now but the worker holds a subscription
        // to event 200 — this is the "Friday practice → Saturday race" boundary. Must
        // release the lease so the next tick can acquire the new event.
        var action = RedMistLeaseDecision.Decide(currentlyHeld: true, heldEventId: 200, InWindowCandidate);
        Assert.AreEqual(LeaseAction.DetachEventChanged, action);
    }

    [TestMethod]
    public void Held_HeldEventIdMissing_TreatedAsDifferentEvent()
    {
        // Defensive: held=true but heldEventId=null shouldn't happen in production, but if it
        // ever does (state bug, partial restore), the safest move is to detach as
        // event-changed rather than silently renew on a mismatched session.
        var action = RedMistLeaseDecision.Decide(currentlyHeld: true, heldEventId: null, InWindowCandidate);
        Assert.AreEqual(LeaseAction.DetachEventChanged, action);
    }
}
