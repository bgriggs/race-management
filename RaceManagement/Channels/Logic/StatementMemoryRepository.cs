using System;
using System.Collections.Generic;
using System.Text;

namespace Channels.Logic;

public class StatementMemoryRepository : IStatementRepository
{
    private readonly Dictionary<int, Statements> statements = [];

    public Task<Statements> GetStatementsAsync(int statementsId)
    {
        _ = statements.TryGetValue(statementsId, out Statements? s);
        s ??= new Statements { Id = statementsId };
        return Task.FromResult(s);
    }

    public Task SetStatementsAsync(Statements s)
    {
        statements[s.Id] = s;
        return Task.CompletedTask;
    }
}
