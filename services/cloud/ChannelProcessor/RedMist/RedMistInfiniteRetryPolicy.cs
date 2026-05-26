using Microsoft.AspNetCore.SignalR.Client;

namespace ChannelProcessor.RedMist;

/// <summary>
/// SignalR <see cref="IRetryPolicy"/> that never gives up. Cap individual delays at 30 s so
/// failover after a server-side hiccup is bounded. The lease loop in
/// <see cref="RedMistConsumerWorker"/> will release the lease independently if the
/// reconnect drags on past the lease TTL.
/// </summary>
internal sealed class RedMistInfiniteRetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan[] Schedule =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
    ];

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        var idx = (int)Math.Min(retryContext.PreviousRetryCount, Schedule.Length - 1);
        return idx < Schedule.Length ? Schedule[idx] : TimeSpan.FromSeconds(30);
    }
}
