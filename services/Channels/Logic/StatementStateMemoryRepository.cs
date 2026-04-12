namespace Channels.Logic;

public class StatementStateMemoryRepository : IStatementStateRepository
{
    private readonly Dictionary<Guid, bool> states = [];
    private static readonly SemaphoreSlim statesLock = new(1);

    public bool? GetState(Guid statementId) =>
        states.TryGetValue(statementId, out var state) ? state : null;

    public async Task<bool?> GetStateAsync(Guid statementId)
    {
        await statesLock.WaitAsync();
        try
        {
            return states.TryGetValue(statementId, out var state) ? state : null;
        }
        finally
        {
            statesLock.Release();
        }
    }

    public async Task SetStateAsync(Guid statementId, bool state)
    {
        await statesLock.WaitAsync();
        try
        {
            states[statementId] = state;
        }
        finally
        {
            statesLock.Release();
        }
    }
}
