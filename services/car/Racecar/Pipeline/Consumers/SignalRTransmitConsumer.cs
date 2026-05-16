using Channels;
using Microsoft.Extensions.Logging;
using Racecar.Pipeline.Dispatch;

namespace Racecar.Pipeline.Consumers;

/// <summary>
/// Channel consumer that ships values to a SignalR target on a 100 ms delta
/// cadence and a 2.5 s full-state cadence. Two instances are wired into the
/// pipeline (cloud, pit-laptop) parameterised by <see cref="ISignalRTarget"/>.
/// </summary>
public sealed class SignalRTransmitConsumer : IChannelConsumer, IAsyncDisposable
{
    private static readonly TimeSpan DeltaInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan FullInterval = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(2);
    private const int ReconnectFailureThreshold = 5;

    private readonly ISignalRTarget _target;
    private readonly Func<ActiveConfiguration> _configAccessor;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    private readonly object _stateLock = new();
    private readonly Dictionary<int, ChannelValue> _full = new();
    private readonly HashSet<int> _changes = new();

    private CancellationTokenSource? _cts;
    private Task? _deltaLoop;
    private Task? _fullLoop;
    private int _consecutiveSendFailures;
    private long _sendTimeouts;

    public SignalRTransmitConsumer(
        ISignalRTarget target,
        Func<ActiveConfiguration> configAccessor,
        TimeProvider time,
        ILogger logger,
        ChannelConsumerOptions? options = null)
    {
        _target = target;
        _configAccessor = configAccessor;
        _time = time;
        _logger = logger;
        Options = options ?? new ChannelConsumerOptions();
        Name = $"signalr:{target.Name}";
    }

    public string Name { get; }
    public ChannelConsumerOptions Options { get; }

    public long SendTimeouts => Interlocked.Read(ref _sendTimeouts);

    public IReadOnlySet<int>? GetSubscriptions(ActiveConfiguration config) => null; // all channels

    public ValueTask HandleAsync(ReadOnlyMemory<InternalChannelValue> values, CancellationToken ct)
    {
        var config = _configAccessor();
        var span = values.Span;
        lock (_stateLock)
        {
            for (var i = 0; i < span.Length; i++)
            {
                ref readonly var v = ref span[i];
                if (!config.Channels.TryGetValue(v.ChannelId, out var def))
                {
                    continue;
                }
                if (!_full.TryGetValue(v.ChannelId, out var existing))
                {
                    existing = new ChannelValue { SessionIndex = (ushort)v.ChannelId };
                    _full[v.ChannelId] = existing;
                }
                existing.SetBaseValue(v.BaseValue);
                existing.Timestamp = v.WallTime;
                _ = _changes.Add(v.ChannelId);
            }
        }
        return ValueTask.CompletedTask;
    }

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _target.Reconnected += OnReconnected;
        _deltaLoop = Task.Run(() => DeltaLoopAsync(_cts.Token));
        _fullLoop = Task.Run(() => FullLoopAsync(_cts.Token));
    }

    /// <summary>Clears all transmitted state — invoked on configuration reload.</summary>
    public void ResetState()
    {
        lock (_stateLock)
        {
            _full.Clear();
            _changes.Clear();
        }
    }

    private async Task DeltaLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(DeltaInterval, _time, ct).ConfigureAwait(false);
                if (!_target.IsConnected) continue;

                var batch = TakeChanges();
                if (batch.Count == 0) continue;
                await SendWithTimeoutAsync(_target.SendDeltaAsync(batch, ct), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task FullLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(FullInterval, _time, ct).ConfigureAwait(false);
                if (!_target.IsConnected) continue;

                var snapshot = SnapshotFull();
                if (snapshot.Count == 0) continue;
                await SendWithTimeoutAsync(_target.SendFullAsync(snapshot, ct), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void OnReconnected()
    {
        // Send a full snapshot immediately, then clear the change set.
        _ = Task.Run(async () =>
        {
            try
            {
                var ct = _cts?.Token ?? CancellationToken.None;
                var snapshot = SnapshotFull();
                if (snapshot.Count > 0)
                {
                    await SendWithTimeoutAsync(_target.SendFullAsync(snapshot, ct), ct).ConfigureAwait(false);
                }
                lock (_stateLock) { _changes.Clear(); }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Full-on-reconnect send failed for {Target}.", _target.Name);
            }
        });
    }

    private List<ChannelValue> TakeChanges()
    {
        lock (_stateLock)
        {
            if (_changes.Count == 0) return [];
            var list = new List<ChannelValue>(_changes.Count);
            foreach (var id in _changes)
            {
                if (_full.TryGetValue(id, out var v)) list.Add(v);
            }
            _changes.Clear();
            return list;
        }
    }

    private List<ChannelValue> SnapshotFull()
    {
        lock (_stateLock)
        {
            return _full.Count == 0 ? [] : new List<ChannelValue>(_full.Values);
        }
    }

    private async Task SendWithTimeoutAsync(Task send, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var timeout = Task.Delay(SendTimeout, _time, cts.Token);
        var winner = await Task.WhenAny(send, timeout).ConfigureAwait(false);

        if (winner == timeout)
        {
            Interlocked.Increment(ref _sendTimeouts);
            var fails = Interlocked.Increment(ref _consecutiveSendFailures);
            _logger.LogWarning("SignalR send to {Target} timed out (consecutiveFailures={Fails}).",
                _target.Name, fails);
            if (fails >= ReconnectFailureThreshold)
            {
                _logger.LogWarning("SignalR target {Target} exceeded failure threshold; consider reconnect.",
                    _target.Name);
                Interlocked.Exchange(ref _consecutiveSendFailures, 0);
            }
            return;
        }

        cts.Cancel();
        try
        {
            await send.ConfigureAwait(false);
            Interlocked.Exchange(ref _consecutiveSendFailures, 0);
        }
        catch (Exception ex)
        {
            var fails = Interlocked.Increment(ref _consecutiveSendFailures);
            _logger.LogWarning(ex, "SignalR send to {Target} failed (consecutiveFailures={Fails}).",
                _target.Name, fails);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _target.Reconnected -= OnReconnected;
        if (_cts is null) return;
        _cts.Cancel();
        try
        {
            if (_deltaLoop is not null) await _deltaLoop.ConfigureAwait(false);
            if (_fullLoop is not null) await _fullLoop.ConfigureAwait(false);
        }
        catch { /* shutdown */ }
        _cts.Dispose();
        _cts = null;
    }
}
