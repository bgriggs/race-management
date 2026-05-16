using Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Racecar.Clients;
using Racecar.Pipeline.Dispatch;

namespace Racecar.Pipeline.Consumers;

/// <summary>
/// Channel consumer that ships values to the cloud via <see cref="ICloudClient"/> on a
/// 100 ms delta cadence and a 2.5 s full-state cadence.
/// </summary>
public sealed class SignalRTransmitConsumer : IChannelConsumer, IAsyncDisposable
{
    private static readonly TimeSpan DeltaInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan FullInterval = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(2);
    private const int ReconnectFailureThreshold = 5;

    private readonly ICloudClient _cloudClient;
    private readonly Func<ActiveConfiguration> _configAccessor;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    private readonly Lock _stateLock = new();
    private readonly Dictionary<int, ChannelValue> _full = [];
    private readonly HashSet<int> _changes = [];

    private CancellationTokenSource? _cts;
    private Task? _deltaLoop;
    private Task? _fullLoop;
    private int _consecutiveSendFailures;
    private long _sendTimeouts;

    public SignalRTransmitConsumer(
        ICloudClient cloudClient,
        Func<ActiveConfiguration> configAccessor,
        TimeProvider time,
        ILogger logger,
        ChannelConsumerOptions? options = null)
    {
        _cloudClient = cloudClient;
        _configAccessor = configAccessor;
        _time = time;
        _logger = logger;
        Options = options ?? new ChannelConsumerOptions();
        Name = "signalr:cloud";
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
                    existing = new ChannelValue
                    {
                        SessionIndex = (ushort)v.ChannelId
                    };
                    _full[v.ChannelId] = existing;
                }
                existing.Value = v.Value.ToString();
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
        _cloudClient.ConnectionStatusChanged += OnConnectionStatusChanged;
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

                var batch = TakeChanges();
                if (batch.Count == 0) continue;
                await SendWithTimeoutAsync(_cloudClient.SendChannelValuesAsync([.. batch]), ct).ConfigureAwait(false);
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

                var snapshot = SnapshotFull();
                if (snapshot.Count == 0) continue;
                await SendWithTimeoutAsync(_cloudClient.SendChannelValuesAsync([.. snapshot]), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void OnConnectionStatusChanged(HubConnectionState state)
    {
        if (state != HubConnectionState.Connected) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var ct = _cts?.Token ?? CancellationToken.None;
                var snapshot = SnapshotFull();
                if (snapshot.Count > 0)
                {
                    await SendWithTimeoutAsync(_cloudClient.SendChannelValuesAsync([.. snapshot]), ct).ConfigureAwait(false);
                }
                lock (_stateLock) { _changes.Clear(); }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Full-on-reconnect send failed for cloud target.");
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
            return _full.Count == 0 ? [] : [.. _full.Values];
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
            _logger.LogWarning("SignalR send to cloud timed out (consecutiveFailures={Fails}).", fails);
            if (fails >= ReconnectFailureThreshold)
            {
                _logger.LogWarning("SignalR cloud target exceeded failure threshold; consider reconnect.");
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
            _logger.LogWarning(ex, "SignalR send to cloud failed (consecutiveFailures={Fails}).", fails);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cloudClient.ConnectionStatusChanged -= OnConnectionStatusChanged;
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

