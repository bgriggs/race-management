using Microsoft.Extensions.Logging;
using Racecar.CanBus;

namespace Racecar.Pipeline.Dispatch;

/// <summary>
/// Per-consumer pump that drains a mailbox, invokes the consumer handler in
/// try/catch, tracks health, and trips a circuit breaker after repeated
/// handler failures.
/// </summary>
internal abstract class ConsumerHost
{
    private const int CircuitBreakerThreshold = 5;
    private const int CircuitBreakerCooldownMs = 5000;

    private readonly TimeProvider _time;
    private long _processed;
    private long _errors;
    private int _consecutiveFailures;
    private long _unhealthySinceMs;

    protected ConsumerHost(string name, TimeProvider time, ILogger logger)
    {
        Name = name;
        _time = time;
        Logger = logger;
    }

    public string Name { get; }

    public bool Healthy => Volatile.Read(ref _consecutiveFailures) < CircuitBreakerThreshold;

    public long Processed => Interlocked.Read(ref _processed);
    public long Errors => Interlocked.Read(ref _errors);

    /// <summary>Diagnostic accessor for the watchdog (resets per tick).</summary>
    public long ConsumeProcessedDelta() => Interlocked.Exchange(ref _processed, 0);

    public abstract long Dropped { get; }

    /// <summary>Mark the consumer unhealthy from outside (e.g., watchdog hang detection).</summary>
    public void ForceUnhealthy()
    {
        Interlocked.Exchange(ref _consecutiveFailures, CircuitBreakerThreshold);
        Volatile.Write(ref _unhealthySinceMs, _time.GetTimestamp());
    }

    public abstract Task RunAsync(CancellationToken ct);
    public abstract void Complete();

    protected ILogger Logger { get; }

    protected async ValueTask InvokeAsync(Func<CancellationToken, ValueTask> call, CancellationToken ct)
    {
        if (!Healthy && !TryRecover()) return; // circuit open: drain-and-drop
        try
        {
            await call(ct).ConfigureAwait(false);
            Interlocked.Increment(ref _processed);
            Interlocked.Exchange(ref _consecutiveFailures, 0);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _errors);
            var fails = Interlocked.Increment(ref _consecutiveFailures);
            if (fails == CircuitBreakerThreshold)
            {
                Volatile.Write(ref _unhealthySinceMs, _time.GetTimestamp());
                Logger.LogError(ex,
                    "Consumer '{Consumer}' tripped circuit breaker after {Fails} consecutive failures.",
                    Name, fails);
            }
            else
            {
                Logger.LogWarning(ex, "Consumer '{Consumer}' handler threw.", Name);
            }
        }
    }

    private bool TryRecover()
    {
        var since = Volatile.Read(ref _unhealthySinceMs);
        if (since == 0) return false;
        var elapsedMs = _time.GetElapsedTime(since).TotalMilliseconds;
        if (elapsedMs < CircuitBreakerCooldownMs) return false;

        Interlocked.Exchange(ref _consecutiveFailures, 0);
        Volatile.Write(ref _unhealthySinceMs, 0);
        Logger.LogInformation("Consumer '{Consumer}' circuit breaker reset after cooldown.", Name);
        return true;
    }
}

/// <summary>Host pump for raw-frame consumers.</summary>
internal sealed class RawConsumerHost : ConsumerHost
{
    private readonly IRawFrameConsumer _consumer;
    private readonly BoundedMailbox<CanMessage> _mailbox;

    public RawConsumerHost(IRawFrameConsumer consumer, TimeProvider time, ILogger logger)
        : base(consumer.Name, time, logger)
    {
        _consumer = consumer;
        _mailbox = new BoundedMailbox<CanMessage>(
            consumer.Options.Capacity,
            dropOldest: consumer.Options.Mode == RawConsumerMode.DropOldest);
    }

    public override long Dropped => _mailbox.Dropped;

    /// <summary>Non-blocking deliver from the pipeline worker.</summary>
    public void Deliver(CanMessage frame) => _mailbox.Write(frame);

    public override async Task RunAsync(CancellationToken ct)
    {
        await foreach (var frame in _mailbox.ReadAllAsync(ct).ConfigureAwait(false))
        {
            await InvokeAsync(c => _consumer.HandleAsync(frame, c), ct).ConfigureAwait(false);
        }
    }

    public override void Complete() => _mailbox.Complete();
}

/// <summary>Host pump for channel-value consumers.</summary>
internal sealed class ChannelConsumerHost : ConsumerHost
{
    private readonly IChannelConsumer _consumer;
    private readonly CoalescingMailbox? _coalescing;
    private readonly BoundedMailbox<InternalChannelValue[]>? _bounded;
    private IReadOnlySet<int>? _subscriptions;

    public ChannelConsumerHost(
        IChannelConsumer consumer,
        ActiveConfiguration initialConfig,
        TimeProvider time,
        ILogger logger)
        : base(consumer.Name, time, logger)
    {
        _consumer = consumer;
        _subscriptions = consumer.GetSubscriptions(initialConfig);

        switch (consumer.Options.Mode)
        {
            case ChannelConsumerMode.CoalesceLatestPerChannel:
                _coalescing = new CoalescingMailbox(consumer.Options.Capacity);
                break;
            case ChannelConsumerMode.DropOldest:
                _bounded = new BoundedMailbox<InternalChannelValue[]>(consumer.Options.Capacity, dropOldest: true);
                break;
            case ChannelConsumerMode.Lossless:
                _bounded = new BoundedMailbox<InternalChannelValue[]>(consumer.Options.Capacity, dropOldest: false);
                break;
        }
    }

    public override long Dropped => _coalescing?.Dropped ?? _bounded?.Dropped ?? 0;

    public void UpdateSubscriptions(ActiveConfiguration config) =>
        _subscriptions = _consumer.GetSubscriptions(config);

    public bool Subscribes(int channelId) =>
        _subscriptions is null || _subscriptions.Contains(channelId);

    /// <summary>
    /// Non-blocking deliver. The caller is the pipeline worker; this never blocks.
    /// </summary>
    public void Deliver(ReadOnlySpan<InternalChannelValue> values)
    {
        if (values.Length == 0) return;

        if (_coalescing is not null)
        {
            for (var i = 0; i < values.Length; i++)
            {
                ref readonly var v = ref values[i];
                if (Subscribes(v.ChannelId)) _coalescing.Write(in v);
            }
            return;
        }

        // Bounded: filter by subscription, then deliver the surviving batch.
        InternalChannelValue[]? subset = null;
        if (_subscriptions is null)
        {
            subset = values.ToArray();
        }
        else
        {
            var count = 0;
            for (var i = 0; i < values.Length; i++)
                if (_subscriptions.Contains(values[i].ChannelId)) count++;
            if (count == 0) return;
            subset = new InternalChannelValue[count];
            var w = 0;
            for (var i = 0; i < values.Length; i++)
            {
                ref readonly var v = ref values[i];
                if (_subscriptions.Contains(v.ChannelId)) subset[w++] = v;
            }
        }
        _ = _bounded!.Write(subset);
    }

    public override async Task RunAsync(CancellationToken ct)
    {
        if (_coalescing is not null)
        {
            while (!ct.IsCancellationRequested)
            {
                InternalChannelValue[] batch;
                try
                {
                    batch = await _coalescing.DrainAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                if (batch.Length == 0) continue;
                await InvokeAsync(c => _consumer.HandleAsync(batch, c), ct).ConfigureAwait(false);
            }
            return;
        }

        await foreach (var batch in _bounded!.ReadAllAsync(ct).ConfigureAwait(false))
        {
            await InvokeAsync(c => _consumer.HandleAsync(batch, c), ct).ConfigureAwait(false);
        }
    }

    public override void Complete()
    {
        _coalescing?.Complete();
        _bounded?.Complete();
    }
}
