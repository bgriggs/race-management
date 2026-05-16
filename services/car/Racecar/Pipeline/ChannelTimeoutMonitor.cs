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

    private void CheckTimeouts()
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

            if (!_state.TryGet(channelId, out var current))
            {
                // No value seen yet; seed with DefaultValue so the timeout clock starts.
                var seed = new InternalChannelValue(channelId, def.DefaultValue,
                    _time.GetTimestamp(), now);
                _state.Set(in seed);
                continue;
            }

            var elapsedMs = (now - current.WallTime).TotalMilliseconds;
            if (elapsedMs < def.TimeoutMs) continue;

            var reset = new InternalChannelValue(channelId, def.DefaultValue,
                _time.GetTimestamp(), now);

            // Always advance WallTime so the timeout does not re-fire on every tick
            // once the channel is already at DefaultValue.
            _state.Set(in reset);

            // Only notify consumers when the value actually changes.
            if (current.Value == def.DefaultValue) continue;

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
