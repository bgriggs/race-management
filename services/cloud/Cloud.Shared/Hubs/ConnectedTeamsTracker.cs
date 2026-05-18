using System.Collections.Concurrent;

namespace Cloud.Shared.Hubs;

/// <summary>
/// Tracks which teams have at least one active WebHub connection on this replica.
/// Background services use <see cref="GetConnectedTeams"/> to decide which teams to
/// fetch state for when broadcasting periodic snapshots.
/// </summary>
public interface IConnectedTeamsTracker
{
    void Add(int teamId);
    void Remove(int teamId);
    IReadOnlyCollection<int> GetConnectedTeams();
}

public class ConnectedTeamsTracker : IConnectedTeamsTracker
{
    private readonly ConcurrentDictionary<int, int> _counts = new();

    public void Add(int teamId) =>
        _counts.AddOrUpdate(teamId, 1, (_, current) => current + 1);

    public void Remove(int teamId)
    {
        _counts.AddOrUpdate(teamId, 0, (_, current) => current - 1);
        // Drop the entry when no connections remain, but guard against races.
        if (_counts.TryGetValue(teamId, out var c) && c <= 0)
            _counts.TryRemove(KeyValuePair.Create(teamId, c));
    }

    public IReadOnlyCollection<int> GetConnectedTeams() => _counts.Keys.ToArray();
}
