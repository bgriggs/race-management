namespace ChannelProcessor.RedMist;

/// <summary>
/// Pure per-team decision function for <see cref="RedMistConsumerWorker"/>'s tick loop.
/// Given the worker's current in-memory state for a team and the latest activation
/// candidate, returns the single action the worker should take this tick. Extracted from
/// the worker so the decision table can be unit-tested without Redis, HTTP, or timing.
/// </summary>
public static class RedMistLeaseDecision
{
    /// <summary>
    /// Picks the action for one team this tick.
    /// </summary>
    /// <param name="currentlyHeld">Does the worker currently hold the lease for this team in memory?</param>
    /// <param name="heldEventId">The event the in-memory session is subscribed to, if any.</param>
    /// <param name="candidate">The activation rule's verdict for this team at <c>nowUtc</c>; <c>null</c> when no race qualifies (any race exists but none in the time window) or when no paired race exists at all.</param>
    public static LeaseAction Decide(bool currentlyHeld, int? heldEventId, ActivationCandidate? candidate)
    {
        // No qualifying race — never subscribe; release if currently held.
        if (candidate is null || !candidate.InWindow)
        {
            return currentlyHeld
                ? (candidate is null ? LeaseAction.DetachNoCandidate : LeaseAction.DetachOutOfWindow)
                : LeaseAction.PublishNoEventPaired;
        }

        if (!currentlyHeld)
            return LeaseAction.TryAcquire;

        // Holding a lease but the candidate event no longer matches what we subscribed to:
        // teams that pair multiple races back-to-back can transition here at the boundary.
        if (heldEventId != candidate.RedMistEventId)
            return LeaseAction.DetachEventChanged;

        // Same event, still in window: renew our existing lease.
        return LeaseAction.Renew;
    }
}

/// <summary>
/// Per-tick action the worker takes for one team. Mutually exclusive.
/// </summary>
public enum LeaseAction
{
    /// <summary>No paired race in the activation window AND we don't hold a lease — just
    /// republish the "no event paired" status if it changed.</summary>
    PublishNoEventPaired,
    /// <summary>We don't hold a lease, but a candidate is in the activation window — attempt SETNX.</summary>
    TryAcquire,
    /// <summary>We hold the lease for the matching event — renew TTL.</summary>
    Renew,
    /// <summary>We held a lease but no paired race exists any more — release.</summary>
    DetachNoCandidate,
    /// <summary>We held a lease but the candidate's race is now outside the activation window — release.</summary>
    DetachOutOfWindow,
    /// <summary>We held a lease for one event, but the activation rule now picks a different
    /// event for the team — release the old subscription so the next tick can acquire the new one.</summary>
    DetachEventChanged,
}
