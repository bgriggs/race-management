using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Racecar.CanBus;

namespace Racecar.Pipeline;

/// <summary>
/// Bridge between an <see cref="ICanBus"/> and the pipeline worker. Captures
/// the arrival monotonic timestamp and writes the frame to a bounded
/// reader-handoff channel. Writes are non-blocking; on overflow the oldest
/// frame is dropped and counted.
/// </summary>
public sealed class CanBusReader : IAsyncDisposable
{
    /// <summary>Reader-handoff capacity (~250 ms at 4 k msg/s).</summary>
    public const int DefaultCapacity = 1024;

    private readonly int _busIndex;
    private readonly ICanBus _bus;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly Channel<TimestampedFrame> _channel;
    private long _dropped;
    private long _received;
    private bool _subscribed;

    public CanBusReader(int busIndex, ICanBus bus, TimeProvider time, ILogger logger, int capacity = DefaultCapacity)
    {
        _busIndex = busIndex;
        _bus = bus;
        _time = time;
        _logger = logger;
        _channel = Channel.CreateBounded<TimestampedFrame>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public int BusIndex => _busIndex;
    public long Dropped => Interlocked.Read(ref _dropped);
    public long Received => Interlocked.Read(ref _received);

    public ChannelReader<TimestampedFrame> Reader => _channel.Reader;

    public void Start()
    {
        if (_subscribed) return;
        _bus.Received += OnReceived;
        _subscribed = true;
    }

    public void Stop()
    {
        if (!_subscribed) return;
        _bus.Received -= OnReceived;
        _subscribed = false;
        _channel.Writer.TryComplete();
    }

    private void OnReceived(CanMessage message)
    {
        var monotonic = _time.GetTimestamp();
        var wall = _time.GetUtcNow().UtcDateTime;
        Interlocked.Increment(ref _received);

        // Bounded DropOldest channel: TryWrite always succeeds unless completed.
        // Infer a drop when the channel is already at capacity at write time.
        var wasAtCapacity = _channel.Reader.Count >= DefaultCapacity;
        var frame = new TimestampedFrame(_busIndex, message, monotonic, wall);
        if (!_channel.Writer.TryWrite(frame))
        {
            Interlocked.Increment(ref _dropped);
            return;
        }
        if (wasAtCapacity)
        {
            Interlocked.Increment(ref _dropped);
        }
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Frame plus the timestamps captured at arrival into the pipeline.
/// </summary>
public readonly record struct TimestampedFrame(
    int BusIndex,
    CanMessage Frame,
    long MonotonicTicks,
    DateTime WallTime);
