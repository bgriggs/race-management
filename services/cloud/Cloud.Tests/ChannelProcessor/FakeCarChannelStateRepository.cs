using ChannelProcessor.Telemetry;
using Cloud.Shared.Telemetry;

namespace Cloud.Tests.ChannelProcessor;

/// <summary>
/// In-memory test double for <see cref="ICarChannelStateRepository"/>.
/// Mirrors the real change-detection semantics (value-string equality) and
/// records every call so tests can assert on it without mocking Redis.
/// </summary>
internal sealed class FakeCarChannelStateRepository : ICarChannelStateRepository
{
    private readonly Dictionary<(string carKey, ushort sessionIndex), ChannelValueSnapshot> _store = new();

    // --- Call records -------------------------------------------------------

    public record SetIfChangedCall(string CarKey, ushort SessionIndex, ChannelValueSnapshot Snapshot);

    public List<SetIfChangedCall> SetIfChangedCalls { get; } = [];
    public List<string> ClearedCarKeys { get; } = [];

    // --- ICarChannelStateRepository -----------------------------------------

    public Task<bool> SetIfChangedAsync(string carKey, ushort sessionIndex, ChannelValueSnapshot incoming,
        CancellationToken ct = default)
    {
        SetIfChangedCalls.Add(new SetIfChangedCall(carKey, sessionIndex, incoming));

        var key = (carKey, sessionIndex);
        var changed = !_store.TryGetValue(key, out var existing) || existing.Value != incoming.Value;
        if (changed)
            _store[key] = incoming;

        return Task.FromResult(changed);
    }

    public Task<Dictionary<ushort, ChannelValueSnapshot>> GetAllAsync(string carKey, CancellationToken ct = default)
    {
        var result = _store
            .Where(kv => kv.Key.carKey == carKey)
            .ToDictionary(kv => kv.Key.sessionIndex, kv => kv.Value);

        return Task.FromResult(result);
    }

    public Task ClearAsync(string carKey, CancellationToken ct = default)
    {
        ClearedCarKeys.Add(carKey);

        foreach (var key in _store.Keys.Where(k => k.carKey == carKey).ToList())
            _store.Remove(key);

        return Task.CompletedTask;
    }

    // --- Helpers for assertions ---------------------------------------------

    public bool WasSetIfChangedCalledWith(string carKey, ushort sessionIndex, string value) =>
        SetIfChangedCalls.Any(c => c.CarKey == carKey && c.SessionIndex == sessionIndex && c.Snapshot.Value == value);
}
