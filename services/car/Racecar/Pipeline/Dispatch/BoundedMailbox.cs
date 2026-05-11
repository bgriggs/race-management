using System.Threading.Channels;

namespace Racecar.Pipeline.Dispatch;

/// <summary>
/// Bounded mailbox built on <see cref="System.Threading.Channels.Channel{T}"/>.
/// Supports drop-oldest and lossless (fail-fast on full) modes. Writes are
/// non-blocking; the CAN reader thread is never allowed to block.
/// </summary>
internal sealed class BoundedMailbox<T>
{
    private readonly Channel<T> _channel;
    private long _dropped;
    private long _written;

    public BoundedMailbox(int capacity, bool dropOldest)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = dropOldest
                ? BoundedChannelFullMode.DropOldest
                : BoundedChannelFullMode.Wait, // Wait + TryWrite returns false on full = fail-fast.
            SingleReader = true,
            SingleWriter = false,
        };
        Capacity = capacity;
        DropOldest = dropOldest;
        _channel = Channel.CreateBounded<T>(options);
    }

    public int Capacity { get; }
    public bool DropOldest { get; }
    public long Dropped => Interlocked.Read(ref _dropped);
    public long Written => Interlocked.Read(ref _written);

    /// <summary>
    /// Non-blocking write. Returns false if the item could not be enqueued
    /// (lossless mode, channel full or completed); the drop counter is
    /// incremented in either drop case.
    /// </summary>
    public bool Write(T item)
    {
        if (DropOldest)
        {
            // DropOldest BoundedChannel always accepts; the discarded prior item
            // is silently dropped. We can't observe it directly, so we infer a
            // drop when the channel reports full just before the write succeeds.
            var beforeFull = _channel.Reader.Count >= Capacity;
            var ok = _channel.Writer.TryWrite(item);
            if (ok)
            {
                Interlocked.Increment(ref _written);
                if (beforeFull) Interlocked.Increment(ref _dropped);
                return true;
            }
            Interlocked.Increment(ref _dropped);
            return false;
        }

        if (_channel.Writer.TryWrite(item))
        {
            Interlocked.Increment(ref _written);
            return true;
        }
        Interlocked.Increment(ref _dropped);
        return false;
    }

    public void Complete() => _channel.Writer.TryComplete();

    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
