using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Racecar.Pipeline.Dispatch;

namespace Racecar.Pipeline;

/// <summary>
/// Drains the reader-handoff channel and runs pipeline stages 2–7 synchronously
/// per frame. Single worker preserves per-CAN-ID ordering naturally.
/// </summary>
internal sealed class PipelineWorker
{
    private const int MaxValuesPerFrame = 32;

    private readonly ChannelReader<TimestampedFrame> _input;
    private readonly Func<ActiveConfiguration> _configAccessor;
    private readonly CanDecoder _decoder;
    private readonly ChangeFilter _changeFilter;
    private readonly Func<IReadOnlyList<RawConsumerHost>> _rawConsumersAccessor;
    private readonly Func<IReadOnlyList<ChannelConsumerHost>> _channelConsumersAccessor;
    private readonly ILogger _logger;

    private long _framesReceived;
    private long _framesUnmapped;
    private long _decodeErrors;
    private long _deriveErrors;

    public PipelineWorker(
        ChannelReader<TimestampedFrame> input,
        Func<ActiveConfiguration> configAccessor,
        CanDecoder decoder,
        ChangeFilter changeFilter,
        Func<IReadOnlyList<RawConsumerHost>> rawConsumersAccessor,
        Func<IReadOnlyList<ChannelConsumerHost>> channelConsumersAccessor,
        ILogger logger)
    {
        _input = input;
        _configAccessor = configAccessor;
        _decoder = decoder;
        _changeFilter = changeFilter;
        _rawConsumersAccessor = rawConsumersAccessor;
        _channelConsumersAccessor = channelConsumersAccessor;
        _logger = logger;
    }

    public long FramesReceived => Interlocked.Read(ref _framesReceived);
    public long FramesUnmapped => Interlocked.Read(ref _framesUnmapped);
    public long DecodeErrors => Interlocked.Read(ref _decodeErrors);
    public long DeriveErrors => Interlocked.Read(ref _deriveErrors);

    public async Task RunAsync(CancellationToken ct)
    {
        var decoded = new InternalChannelValue[MaxValuesPerFrame];

        await foreach (var frame in _input.ReadAllAsync(ct).ConfigureAwait(false))
        {
            try
            {
                ProcessFrame(in frame, decoded);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipeline worker crashed while processing frame; continuing.");
            }
        }
    }

    private void ProcessFrame(in TimestampedFrame frame, InternalChannelValue[] decoded)
    {
        Interlocked.Increment(ref _framesReceived);
        var config = _configAccessor();

        // Stage 2: raw broadcast
        var rawConsumers = _rawConsumersAccessor();
        for (var i = 0; i < rawConsumers.Count; i++)
        {
            rawConsumers[i].Deliver(frame.Frame);
        }

        // Stage 3: decode
        int decodedCount;
        try
        {
            decodedCount = _decoder.Decode(config, in frame, decoded);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _decodeErrors);
            _logger.LogWarning(ex, "Decode error on bus={Bus} id=0x{Id:X}.",
                frame.BusIndex, frame.Frame.CanId);
            return;
        }

        if (decodedCount == 0 && !config.Messages.ContainsKey((frame.BusIndex, frame.Frame.CanId)))
        {
            Interlocked.Increment(ref _framesUnmapped);
            return;
        }

        // Stage 4: change filter on decoded inputs
        var span = decoded.AsSpan(0, decodedCount);
        var keptCount = _changeFilter.Filter(config, span);
        if (keptCount == 0) return;

        var changedInputs = decoded.AsSpan(0, keptCount);

        // Stage 5: derive
        InternalChannelValue[] derived;
        try
        {
            derived = config.DerivedEvaluator.Evaluate(changedInputs, frame.MonotonicTicks, frame.WallTime);
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _deriveErrors);
            _logger.LogWarning(ex, "Derive error on bus={Bus} id=0x{Id:X}.",
                frame.BusIndex, frame.Frame.CanId);
            derived = [];
        }

        // Stage 6: change filter on derived outputs (in-place)
        int derivedKept = 0;
        for (var i = 0; i < derived.Length; i++)
        {
            if (_changeFilter.TryAccept(config, in derived[i]))
            {
                derived[derivedKept++] = derived[i];
            }
        }

        // Stage 7: dispatch
        var channelConsumers = _channelConsumersAccessor();
        if (changedInputs.Length > 0)
        {
            for (var c = 0; c < channelConsumers.Count; c++)
            {
                channelConsumers[c].Deliver(changedInputs);
            }
        }
        if (derivedKept > 0)
        {
            var derivedSpan = derived.AsSpan(0, derivedKept);
            for (var c = 0; c < channelConsumers.Count; c++)
            {
                channelConsumers[c].Deliver(derivedSpan);
            }
        }
    }
}
