using ChannelProcessor.Telemetry;
using Cloud.Shared.Telemetry;

namespace Cloud.Tests.ChannelProcessor;

/// <summary>
/// In-memory test double for <see cref="ITeamChannelStateRepository"/>. Mirrors the
/// real change-detection semantics (value-string equality) and records every call so
/// tests can assert on it without mocking Redis.
/// </summary>
internal sealed class FakeTeamChannelStateRepository : ITeamChannelStateRepository
{
    private readonly Dictionary<(int teamId, Guid channelId), ChannelValueSnapshot> _store = new();

    public record SetIfChangedCall(int TeamId, Guid ChannelId, ChannelValueSnapshot Snapshot);

    public List<SetIfChangedCall> SetIfChangedCalls { get; } = [];

    public Task<bool> SetIfChangedAsync(int teamId, Guid channelId, ChannelValueSnapshot incoming, CancellationToken ct = default)
    {
        SetIfChangedCalls.Add(new SetIfChangedCall(teamId, channelId, incoming));

        var key = (teamId, channelId);
        var changed = !_store.TryGetValue(key, out var existing) || existing.Value != incoming.Value;
        if (changed)
            _store[key] = incoming;

        return Task.FromResult(changed);
    }

    public Task<Dictionary<Guid, ChannelValueSnapshot>> GetAllAsync(int teamId, CancellationToken ct = default)
    {
        var result = _store
            .Where(kv => kv.Key.teamId == teamId)
            .ToDictionary(kv => kv.Key.channelId, kv => kv.Value);

        return Task.FromResult(result);
    }

    public bool WasSetIfChangedCalledWith(int teamId, Guid channelId, string value) =>
        SetIfChangedCalls.Any(c => c.TeamId == teamId && c.ChannelId == channelId && c.Snapshot.Value == value);
}
