namespace Channels.Logic;

public class ComparisonDurationMemoryRepository : IComparisonDurationRepository
{
    private readonly Dictionary<Guid, DateTimeOffset> startTimes = [];
    private static readonly SemaphoreSlim startTimesLock = new(1);

    public async Task<DateTimeOffset?> GetStartTimeAsync(Guid comparisonId)
    {
        await startTimesLock.WaitAsync();
        try
        {
            return startTimes.TryGetValue(comparisonId, out var startTime) ? startTime : null;
        }
        finally
        {
            startTimesLock.Release();
        }
    }

    public async Task SetStartTimeAsync(Guid comparisonId, DateTimeOffset startTime)
    {
        await startTimesLock.WaitAsync();
        try
        {
            startTimes[comparisonId] = startTime;
        }
        finally
        {
            startTimesLock.Release();
        }
    }

    public async Task RemoveStartTimeAsync(Guid comparisonId)
    {
        await startTimesLock.WaitAsync();
        try
        {
            startTimes.Remove(comparisonId);
        }
        finally
        {
            startTimesLock.Release();
        }
    }
}
