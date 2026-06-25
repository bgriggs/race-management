using System.Collections.Concurrent;

namespace Racecar.Pipeline;

/// <summary>
/// Holds the latest published value per channel ID. Updated by the
/// <see cref="ChangeFilter"/> as values pass through, queried by REST snapshot
/// endpoints and any in-process consumer that needs "current value of X."
/// </summary>
/// <remarks>
/// Reads use <see cref="ConcurrentDictionary{TKey, TValue}"/> directly so they
/// never block the pipeline worker, at the cost of not being a single-instant
/// snapshot across channels — fine for status / debug surfaces.
/// </remarks>
public sealed class ChannelStatusState
{
    private readonly ConcurrentDictionary<int, InternalChannelValue> _state = new();

    /// <summary>
    /// Per-channel wall-clock time of the most recently <em>received</em> sample, independent
    /// of whether that sample changed the published value. This is liveness, not the published
    /// value: the change filter drops unchanged samples, so <see cref="_state"/>'s WallTime only
    /// advances on value changes and cannot tell "data stopped" from "value is constant". The
    /// timeout monitor keys off this map so a constant-but-live channel is never reset to default.
    /// </summary>
    private readonly ConcurrentDictionary<int, DateTime> _lastSeen = new();

    /// <summary>Total number of distinct channels currently held.</summary>
    public int Count => _state.Count;

    /// <summary>Get the latest value for <paramref name="channelId"/>, if any.</summary>
    public bool TryGet(int channelId, out InternalChannelValue value) =>
        _state.TryGetValue(channelId, out value);

    /// <summary>
    /// Record that a sample was received for <paramref name="channelId"/> at
    /// <paramref name="wallTime"/>, regardless of whether it changed the value. Called for every
    /// sample the change filter inspects so the timeout monitor can distinguish a live constant
    /// channel from one whose source has gone silent.
    /// </summary>
    internal void MarkSeen(int channelId, DateTime wallTime) => _lastSeen[channelId] = wallTime;

    /// <summary>Wall-clock time of the last sample received for <paramref name="channelId"/>, if any.</summary>
    public bool TryGetLastSeen(int channelId, out DateTime wallTime) =>
        _lastSeen.TryGetValue(channelId, out wallTime);

    /// <summary>Snapshot of all currently-known channel values.</summary>
    public IReadOnlyDictionary<int, InternalChannelValue> Snapshot() =>
        new Dictionary<int, InternalChannelValue>(_state);

    /// <summary>Set or replace the latest value for a channel.</summary>
    internal void Set(in InternalChannelValue value) => _state[value.ChannelId] = value;

    /// <summary>Clear all state. Called on configuration reload.</summary>
    internal void Clear()
    {
        _state.Clear();
        _lastSeen.Clear();
    }

    /// <summary>Remove channels that no longer exist after a config reload.</summary>
    internal void RetainOnly(IReadOnlySet<int> channelIds)
    {
        foreach (var key in _state.Keys)
        {
            if (!channelIds.Contains(key))
            {
                _state.TryRemove(key, out _);
            }
        }
        foreach (var key in _lastSeen.Keys)
        {
            if (!channelIds.Contains(key))
            {
                _lastSeen.TryRemove(key, out _);
            }
        }
    }
}
