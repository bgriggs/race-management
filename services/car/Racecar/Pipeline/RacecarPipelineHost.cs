using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Common;
using Racecar.CanBus;
using Racecar.Pipeline.Dispatch;

namespace Racecar.Pipeline;

/// <summary>
/// Top-level pipeline host. Wires CAN bus readers, the pipeline worker,
/// raw-frame and channel-value consumers, the watchdog, and the writer.
/// Owns the active configuration snapshot and exposes hooks for atomic reload.
/// </summary>
public sealed class RacecarPipelineHost : BackgroundService
{
    private static readonly TimeSpan ShutdownBudget = TimeSpan.FromSeconds(2);

    private readonly IOptionsMonitor<CarConfiguration> _carConfig;
    private readonly IEnumerable<IRawFrameConsumer> _rawConsumers;
    private readonly IEnumerable<IChannelConsumer> _channelConsumers;
    private readonly TimeProvider _time;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<RacecarPipelineHost> _logger;
    private readonly ChannelStatusState _statusState;
    private readonly ChangeFilter _changeFilter;
    private readonly CanDecoder _decoder;

    private readonly List<CanBusReader> _readers = new();
    private readonly List<RawConsumerHost> _rawHosts = new();
    private readonly List<ChannelConsumerHost> _channelHosts = new();

    private volatile ActiveConfiguration _active = ActiveConfiguration.Empty;
    private Channel<TimestampedFrame>? _mergedInput;

    public RacecarPipelineHost(
        IOptionsMonitor<CarConfiguration> carConfig,
        IEnumerable<IRawFrameConsumer> rawConsumers,
        IEnumerable<IChannelConsumer> channelConsumers,
        TimeProvider time,
        ChannelStatusState statusState,
        ILoggerFactory loggerFactory)
    {
        _carConfig = carConfig;
        _rawConsumers = rawConsumers;
        _channelConsumers = channelConsumers;
        _time = time;
        _statusState = statusState;
        _changeFilter = new ChangeFilter(statusState);
        _decoder = new CanDecoder();
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<RacecarPipelineHost>();
    }

    public ActiveConfiguration ActiveConfiguration => _active;

    /// <summary>
    /// Atomic configuration reload. State for channels with byte-identical
    /// definitions carries over; changed/new channels reset.
    /// </summary>
    public void ReloadConfiguration(ActiveConfiguration next)
    {
        var previous = Interlocked.Exchange(ref _active, next);
        MigrateState(previous, next);
        foreach (var host in _channelHosts) host.UpdateSubscriptions(next);
        _logger.LogInformation(
            "Pipeline configuration reloaded: {OldId} -> {NewId} ({Channels} channels).",
            previous.ConfigurationId, next.ConfigurationId, next.Channels.Count);
    }

    private void MigrateState(ActiveConfiguration previous, ActiveConfiguration next)
    {
        var keepIds = new HashSet<int>();
        foreach (var (id, hash) in next.DefinitionHashes)
        {
            if (previous.DefinitionHashes.TryGetValue(id, out var oldHash) && oldHash == hash)
            {
                keepIds.Add(id);
            }
        }
        _statusState.RetainOnly(keepIds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial empty config; the host owner can call ReloadConfiguration once
        // CarConfiguration → ActiveConfiguration mapping is wired up.
        _logger.LogInformation("Racecar pipeline host starting.");

        // Build consumer hosts.
        foreach (var consumer in _rawConsumers)
        {
            _rawHosts.Add(new RawConsumerHost(consumer, _time, _loggerFactory.CreateLogger(consumer.Name)));
        }
        foreach (var consumer in _channelConsumers)
        {
            _channelHosts.Add(new ChannelConsumerHost(
                consumer, _active, _time, _loggerFactory.CreateLogger(consumer.Name)));
        }

        // Merged reader handoff. Bus readers are added externally via AttachBus.
        _mergedInput = Channel.CreateBounded<TimestampedFrame>(new BoundedChannelOptions(CanBusReader.DefaultCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        var worker = new PipelineWorker(
            _mergedInput.Reader,
            () => _active,
            _decoder,
            _changeFilter,
            () => _rawHosts,
            () => _channelHosts,
            _loggerFactory.CreateLogger<PipelineWorker>());

        var watchdog = new Watchdog(_time,
            () => ((IEnumerable<ConsumerHost>)_rawHosts).Concat(_channelHosts),
            _loggerFactory.CreateLogger<Watchdog>());

        var workerTask = worker.RunAsync(stoppingToken);
        var watchdogTask = watchdog.RunAsync(stoppingToken);
        var consumerTasks = _rawHosts.Cast<ConsumerHost>().Concat(_channelHosts)
            .Select(h => h.RunAsync(stoppingToken)).ToList();

        try
        {
            await Task.WhenAny(new[] { workerTask, watchdogTask }.Concat(consumerTasks)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }

        await ShutdownAsync(workerTask, watchdogTask, consumerTasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Attach a CAN bus reader to the pipeline. Forwards frames into the merged
    /// input handoff. Must be called after <see cref="ExecuteAsync"/> has wired
    /// the merged input — typically from a startup orchestrator.
    /// </summary>
    public void AttachBus(int busIndex, ICanBus bus)
    {
        if (_mergedInput is null)
        {
            throw new InvalidOperationException("Pipeline host not started yet.");
        }
        var reader = new CanBusReader(busIndex, bus, _time,
            _loggerFactory.CreateLogger<CanBusReader>());
        _readers.Add(reader);
        reader.Start();

        // Forward this reader's channel into the merged input.
        var fwdToken = CancellationToken.None;
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var frame in reader.Reader.ReadAllAsync(fwdToken).ConfigureAwait(false))
                {
                    _ = _mergedInput.Writer.TryWrite(frame);
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    private async Task ShutdownAsync(Task worker, Task watchdog, List<Task> consumerTasks)
    {
        _logger.LogInformation("Racecar pipeline shutting down (budget {Budget}).", ShutdownBudget);
        foreach (var reader in _readers) reader.Stop();
        _mergedInput?.Writer.TryComplete();

        try { await worker.WaitAsync(ShutdownBudget).ConfigureAwait(false); }
        catch (TimeoutException) { _logger.LogWarning("Pipeline worker did not drain within budget."); }
        catch { /* swallow */ }

        foreach (var host in _rawHosts.Cast<ConsumerHost>().Concat(_channelHosts))
        {
            host.Complete();
        }

        try { await Task.WhenAll(consumerTasks).WaitAsync(ShutdownBudget).ConfigureAwait(false); }
        catch (TimeoutException) { _logger.LogWarning("Consumers did not drain within budget."); }
        catch { /* swallow */ }

        try { await watchdog.WaitAsync(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false); } catch { }
    }
}
