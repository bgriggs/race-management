using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Racecar.CanBus;

namespace Racecar.Pipeline;

public sealed record CanWriteRequest(
    int CanBusIndex,
    uint CanId,
    ReadOnlyMemory<byte> Data,
    string? Source);

public interface ICanWriter
{
    ValueTask EnqueueAsync(CanWriteRequest request, CancellationToken ct = default);
}

/// <summary>
/// Single-task drain of an outbound <see cref="System.Threading.Channels.Channel{T}"/>
/// to <see cref="ICanBus.Send"/>. Bounded; throws on full (writes are not telemetry).
/// </summary>
public sealed class CanWriter : ICanWriter, IAsyncDisposable
{
    private readonly Channel<CanWriteRequest> _outbound;
    private readonly IReadOnlyList<ICanBus> _busesByIndex;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _drain;

    public CanWriter(IReadOnlyList<ICanBus> busesByIndex, ILogger logger, int capacity = 256)
    {
        _busesByIndex = busesByIndex;
        _logger = logger;
        _outbound = Channel.CreateBounded<CanWriteRequest>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ValueTask EnqueueAsync(CanWriteRequest request, CancellationToken ct = default)
    {
        if (request.Data.Length > 8)
        {
            throw new ArgumentException("CAN frame data length must be 0..8 bytes.", nameof(request));
        }
        if (!_outbound.Writer.TryWrite(request))
        {
            throw new InvalidOperationException("CAN write queue is full.");
        }
        return ValueTask.CompletedTask;
    }

    public void Start()
    {
        _drain ??= Task.Run(() => DrainLoopAsync(_cts.Token));
    }

    private async Task DrainLoopAsync(CancellationToken ct)
    {
        await foreach (var req in _outbound.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            try
            {
                if (req.CanBusIndex < 0 || req.CanBusIndex >= _busesByIndex.Count)
                {
                    _logger.LogWarning("CanWriter: unknown bus index {Bus} from {Source}.",
                        req.CanBusIndex, req.Source);
                    continue;
                }

                var bus = _busesByIndex[req.CanBusIndex];
                var bytes = req.Data.ToArray();
                var msg = new CanMessage
                {
                    CanId = req.CanId,
                    Data = bytes,
                    DataLength = bytes.Length,
                    Timestamp = DateTime.UtcNow,
                };
                bus.Send(msg);
                _logger.LogDebug(
                    "CanWriter sent bus={Bus} id=0x{Id:X} len={Len} source={Source}.",
                    req.CanBusIndex, req.CanId, bytes.Length, req.Source);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CanWriter failed to send frame from {Source}.", req.Source);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _outbound.Writer.TryComplete();
        _cts.Cancel();
        if (_drain is not null)
        {
            try { await _drain.ConfigureAwait(false); } catch { /* shutdown */ }
        }
        _cts.Dispose();
    }
}
