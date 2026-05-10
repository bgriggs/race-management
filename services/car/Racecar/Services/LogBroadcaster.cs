using System.Threading.Channels;

namespace Racecar.Services;

/// <summary>
/// Singleton that receives rendered log lines from the NLog broadcast target,
/// maintains a short in-memory history, and fans out to active SSE subscribers.
/// </summary>
public sealed class LogBroadcaster
{
    private const int BufferCapacity = 500;

    private readonly Lock _lock = new();
    private readonly Queue<string> _history = new(BufferCapacity);
    private readonly List<Channel<string>> _subscribers = [];

    public void Publish(string entry)
    {
        lock (_lock)
        {
            if (_history.Count >= BufferCapacity)
                _history.Dequeue();

            _history.Enqueue(entry);

            foreach (var ch in _subscribers)
                ch.Writer.TryWrite(entry);
        }
    }

    public LogSubscription Subscribe()
    {
        var channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true
        });

        string[] snapshot;
        lock (_lock)
        {
            snapshot = [.. _history];
            _subscribers.Add(channel);
        }

        return new LogSubscription(snapshot, channel, Unsubscribe);
    }

    private void Unsubscribe(Channel<string> channel)
    {
        lock (_lock)
        {
            _subscribers.Remove(channel);
            channel.Writer.TryComplete();
        }
    }
}

public sealed class LogSubscription : IDisposable
{
    private readonly Channel<string> _channel;
    private readonly Action<Channel<string>> _unsubscribe;

    public IReadOnlyList<string> History { get; }
    public ChannelReader<string> Reader => _channel.Reader;

    internal LogSubscription(string[] history, Channel<string> channel, Action<Channel<string>> unsubscribe)
    {
        History = history;
        _channel = channel;
        _unsubscribe = unsubscribe;
    }

    public void Dispose() => _unsubscribe(_channel);
}
