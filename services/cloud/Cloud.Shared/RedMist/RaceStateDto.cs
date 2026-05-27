namespace Cloud.Shared.RedMist;

/// <summary>
/// Per-team race-header state pushed over <c>WebHub</c> via
/// <c>IWebHubClient.RaceStateChanged</c>. Sourced exclusively from the RedMist feed —
/// when a team has no active RedMist session, the DTO is <c>null</c> on the wire and
/// the UI renders blanks. Time fields are pass-through strings ("HH:MM:SS") so the
/// timing-board's formatting is preserved without parsing.
/// </summary>
public sealed class RaceStateDto
{
    /// <summary>
    /// RedMist event id currently being consumed by the ChannelProcessor for this team —
    /// not the user-configured <c>Race.RedMistEventId</c>, which may be null for org-only
    /// pairings. Drives the Race-Position iframe URL on the client so the embed always
    /// shows the same event the rest of the header is sourced from.
    /// </summary>
    public int? EventId { get; init; }

    /// <summary>
    /// Wall-clock at the timing board from RedMist's <c>SessionState.LocalTimeOfDay</c>.
    /// The Race-Monitor header displays this instead of the browser's clock so every
    /// engineer sees the same time the timing booth does.
    /// </summary>
    public string? LocalTimeOfDay { get; init; }

    /// <summary>Race elapsed time from RedMist's <c>SessionState.RunningRaceTime</c>.</summary>
    public string? RunningRaceTime { get; init; }

    /// <summary>Remaining race time from RedMist's <c>SessionState.TimeToGo</c>.</summary>
    public string? TimeToGo { get; init; }

    /// <summary>Leader's most recently completed lap (max of <c>CarPosition.LastLapCompleted</c>).</summary>
    public int? LeaderLap { get; init; }

    /// <summary>Mapped race flag (see <c>RedMistFlagMapper</c>), e.g. "Green", "Yellow".</summary>
    public string? Flag { get; init; }

    /// <summary>UTC timestamp of the last update that produced this snapshot.</summary>
    public DateTime LastUpdatedUtc { get; init; }
}
