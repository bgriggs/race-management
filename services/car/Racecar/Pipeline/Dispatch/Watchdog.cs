using Microsoft.Extensions.Logging;

namespace Racecar.Pipeline.Dispatch;

/// <summary>
/// 1 Hz health check across all consumers. Catches consumers that are hung
/// (handler not throwing, but no progress while the mailbox is dropping).
/// </summary>
internal sealed class Watchdog
{
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly Func<IEnumerable<ConsumerHost>> _consumersAccessor;

    public Watchdog(
        TimeProvider time,
        Func<IEnumerable<ConsumerHost>> consumersAccessor,
        ILogger logger)
    {
        _time = time;
        _consumersAccessor = consumersAccessor;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var prevDropped = new Dictionary<string, long>();
        using var timer = _time.CreateTimer(_ => { }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), _time, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }

            foreach (var consumer in _consumersAccessor())
            {
                var processedThisTick = consumer.ConsumeProcessedDelta();
                var droppedNow = consumer.Dropped;
                prevDropped.TryGetValue(consumer.Name, out var droppedPrev);
                var droppedThisTick = droppedNow - droppedPrev;
                prevDropped[consumer.Name] = droppedNow;

                if (droppedThisTick > 0 && processedThisTick == 0 && consumer.Healthy)
                {
                    _logger.LogWarning(
                        "Watchdog: consumer '{Consumer}' appears hung " +
                        "(dropped={Dropped}, processed=0); marking unhealthy.",
                        consumer.Name, droppedThisTick);
                    consumer.ForceUnhealthy();
                }
            }
        }
    }
}
