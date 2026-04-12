namespace Channels.Logic;

public class ComparisonDurationMemoryRepository : IComparisonDurationRepository
{
    private readonly Dictionary<int, DateTimeOffset> startTimes = [];
    private static readonly SemaphoreSlim startTimesLock = new(1);

    public async Task<DateTimeOffset?> GetStartTimeAsync(int comparisonId)
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

    public async Task SetStartTimeAsync(int comparisonId, DateTimeOffset startTime)
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

    public async Task RemoveStartTimeAsync(int comparisonId)
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
