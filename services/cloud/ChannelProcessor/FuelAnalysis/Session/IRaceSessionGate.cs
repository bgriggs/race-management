namespace ChannelProcessor.FuelAnalysis.Session;

/// <summary>
/// Resolves whether a team currently has an active <see cref="Cloud.Shared.Database.Models.Race"/> —
/// the Race whose <c>Start</c>/<c>Start + Duration</c> brackets the current time. The Fuel
/// Reconciler treats this as its session-active gate per design.md §616. The user has
/// chosen to use <c>Race.Id</c> as the session identifier in lieu of a separate
/// RaceSession entity.
/// </summary>
public interface IRaceSessionGate
{
    /// <summary>
    /// Returns the active <c>Race.Id</c> for the team, or <c>null</c> if no race brackets <see cref="DateTime.UtcNow"/>.
    /// Cached briefly to amortize lookup cost across the per-message hot path.
    /// </summary>
    Task<int?> GetActiveRaceIdAsync(int teamId, CancellationToken ct = default);
}
