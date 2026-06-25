using Microsoft.Extensions.Logging;
using Racecar.Pipeline.Dispatch;

namespace Racecar.Pipeline;

/// <summary>
/// Monitors channels that have a non-zero <see cref="Channels.ChannelDefinition.TimeoutMs"/>
/// and resets their value to <see cref="Channels.ChannelDefinition.DefaultValue"/> when no
/// update has been received within the configured window.
/// </summary>
/// <remarks>
/// Runs on its own background task; writes to <see cref="ChannelStatusState"/> and delivers
/// directly to consumers. State writes use <see cref="ConcurrentDictionary"/> operations and
/// consumer delivery uses mailboxes, so concurrent access with the pipeline worker is safe.
/// </remarks>
internal sealed class ChannelTimeoutMonitor
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(100);

    private readonly Func<ActiveConfiguration> _configAccessor;
    private readonly ChannelStatusState _state;
    private readonly Func<IReadOnlyList<ChannelConsumerHost>> _consumersAccessor;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    public ChannelTimeoutMonitor(
        Func<ActiveConfiguration> configAccessor,
        ChannelStatusState state,
        Func<IReadOnlyList<ChannelConsumerHost>> consumersAccessor,
        TimeProvider time,
        ILogger logger)
    {
        _configAccessor = configAccessor;
        _state = state;
        _consumersAccessor = consumersAccessor;
        _time = time;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, _time, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }

            CheckTimeouts();
        }
    }

    /// <summary>
    /// One pass of the timeout sweep. Exposed (internal) so tests can drive it deterministically
    /// without racing the <see cref="RunAsync"/> delay loop.
    /// </summary>
    internal void CheckTimeouts()
    {
        var config = _configAccessor();
        if (config.Channels.Count == 0) return;

        var now = _time.GetUtcNow().UtcDateTime;
        var consumers = _consumersAccessor();

        InternalChannelValue[]? resets = null;
        var resetCount = 0;

        foreach (var (channelId, def) in config.Channels)
        {
            if (def.TimeoutMs <= 0) continue;

            // Liveness drives the timeout, not the published value's WallTime: the change
            // filter only refreshes the published value when it changes, so a constant-but-live
            // channel would otherwise look silent. _lastSeen advances on every received sample.
            if (!_state.TryGetLastSeen(channelId, out var lastSeen))
            {
                // No sample ever received; ensure the published value reads as DefaultValue.
                if (!_state.TryGet(channelId, out _))
                {
                    var seed = new InternalChannelValue(channelId, def.DefaultValue,
                        _time.GetTimestamp(), now);
                    _state.Set(in seed);
                }
                continue;
            }

            var elapsedMs = (now - lastSeen).TotalMilliseconds;
            if (elapsedMs < def.TimeoutMs) continue;

            // Source has gone silent. Only reset/notify if the published value isn't already
            // the default — the value==default guard below also stops this re-firing each tick.
            if (_state.TryGet(channelId, out var current) && current.Value == def.DefaultValue)
            {
                continue;
            }

            var reset = new InternalChannelValue(channelId, def.DefaultValue,
                _time.GetTimestamp(), now);
            _state.Set(in reset);

            _logger.LogDebug(
                "Channel {ChannelId} ({Name}) timed out after {Elapsed:F0} ms; resetting to default {Default}.",
                channelId, def.Name, elapsedMs, def.DefaultValue);

            resets ??= new InternalChannelValue[config.Channels.Count];
            resets[resetCount++] = reset;
        }

        if (resetCount == 0 || consumers.Count == 0) return;

        var span = resets.AsSpan(0, resetCount);
        for (var c = 0; c < consumers.Count; c++)
        {
            consumers[c].Deliver(span);
        }
    }
}
