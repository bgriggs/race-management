using System.Threading.Channels;

namespace Racecar.Pipeline.Dispatch;

/// <summary>
/// Per-channel-ID coalescing mailbox: a new value for an existing channel
/// overwrites an unconsumed older value. The pump drains all currently held
/// values as one batch and hands them to the consumer.
/// </summary>
/// <remarks>
/// Writes are non-blocking and never throw on overflow. <see cref="Capacity"/>
/// bounds the number of distinct channels held; if exceeded, the oldest pending
/// channel entry is evicted and counted in <see cref="Dropped"/>.
/// </remarks>
internal sealed class CoalescingMailbox
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, InternalChannelValue> _pending;
    private readonly Queue<int> _order;
    private readonly Channel<bool> _signal;
    private long _dropped;
    private long _written;

    public CoalescingMailbox(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _pending = new Dictionary<int, InternalChannelValue>(capacity);
        _order = new Queue<int>(capacity);
        _signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public int Capacity { get; }
    public long Dropped => Interlocked.Read(ref _dropped);
    public long Written => Interlocked.Read(ref _written);

    public int PendingCount
    {
        get { lock (_lock) { return _pending.Count; } }
    }

    /// <summary>
    /// Non-blocking write. Always returns immediately; on overflow the oldest
    /// pending channel is evicted and counted.
    /// </summary>
    public void Write(in InternalChannelValue value)
    {
        lock (_lock)
        {
            if (_pending.ContainsKey(value.ChannelId))
            {
                _pending[value.ChannelId] = value;
            }
            else
            {
                if (_pending.Count >= Capacity)
                {
                    var evict = _order.Dequeue();
                    _pending.Remove(evict);
                    Interlocked.Increment(ref _dropped);
                }
                _pending.Add(value.ChannelId, value);
                _order.Enqueue(value.ChannelId);
            }
            Interlocked.Increment(ref _written);
        }
        _ = _signal.Writer.TryWrite(true);
    }

    /// <summary>
    /// Async drain: waits for pending data, then atomically returns all
    /// currently held values as one batch.
    /// </summary>
    public async ValueTask<InternalChannelValue[]> DrainAsync(CancellationToken ct)
    {
        while (true)
        {
            InternalChannelValue[]? batch = TryDrain();
            if (batch is not null) return batch;

            try
            {
                _ = await _signal.Reader.ReadAsync(ct).ConfigureAwait(false);
            }
            catch (ChannelClosedException)
            {
                batch = TryDrain();
                return batch ?? [];
            }
        }
    }

    public void Complete() => _signal.Writer.TryComplete();

    private InternalChannelValue[]? TryDrain()
    {
        lock (_lock)
        {
            if (_pending.Count == 0) return null;
            var batch = new InternalChannelValue[_pending.Count];
            var i = 0;
            foreach (var v in _pending.Values) batch[i++] = v;
            _pending.Clear();
            _order.Clear();
            return batch;
        }
    }
}
