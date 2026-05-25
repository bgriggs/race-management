using System.Text.Json;
using Channels.Alarms;
using Channels.Logic;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models.Alarms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace ChannelProcessor.Alarms.Config;

public sealed class AlarmDefinitionRepository(
    IDbContextFactory<RaceManagementContext> dbFactory,
    HybridCache cache,
    ILogger<AlarmDefinitionRepository> logger) : IAlarmDefinitionRepository
{
    // Alarm definitions are administratively managed (rare writes) but read on every
    // stream message, so a short cache eliminates most DB hits without making config
    // edits feel stuck.
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(2),
        LocalCacheExpiration = TimeSpan.FromMinutes(2),
    };

    public async Task<IReadOnlyList<AlarmDefinition>> GetForCarAsync(int teamId, string carNumber, CancellationToken ct = default)
    {
        var key = CacheKey(teamId, carNumber);
        return await cache.GetOrCreateAsync(
            key,
            (teamId, carNumber, factory: dbFactory, log: logger),
            static async (state, innerCt) =>
            {
                await using var db = await state.factory.CreateDbContextAsync(innerCt);
                var rows = await db.AlarmDefinitions
                    .AsNoTracking()
                    .Where(a => a.TeamId == state.teamId
                        && (a.CarNumber == null || a.CarNumber == state.carNumber))
                    .ToListAsync(innerCt);

                var result = new List<AlarmDefinition>(rows.Count);
                foreach (var row in rows)
                {
                    var statement = DeserializeStatement(row.StatementJson, state.log, row.Id);
                    result.Add(new AlarmDefinition
                    {
                        Id = row.Id,
                        Name = row.Name,
                        Messsage = row.Message,
                        Statement = statement,
                        DisplayChannelSourceColorHex = row.DisplayChannelSourceColorHex,
                        TimeAfterAckToDisplaySecs = row.TimeAfterAckToDisplaySecs,
                        AlarmStatusChannelId = row.AlarmStatusChannelId,
                    });
                }
                return (IReadOnlyList<AlarmDefinition>)result;
            },
            CacheOptions,
            tags: [TeamTag(teamId)],
            cancellationToken: ct);
    }

    public async Task SaveAsync(int teamId, string? carNumber, AlarmDefinition definition, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var existing = await db.AlarmDefinitions.FirstOrDefaultAsync(a => a.Id == definition.Id, ct);
        var statementJson = JsonSerializer.Serialize(definition.Statement);

        if (existing is null)
        {
            db.AlarmDefinitions.Add(new AlarmDefinitionRow
            {
                Id = definition.Id == Guid.Empty ? Guid.NewGuid() : definition.Id,
                TeamId = teamId,
                CarNumber = carNumber,
                Name = definition.Name,
                Message = definition.Messsage,
                DisplayChannelSourceColorHex = definition.DisplayChannelSourceColorHex,
                TimeAfterAckToDisplaySecs = definition.TimeAfterAckToDisplaySecs,
                AlarmStatusChannelId = definition.AlarmStatusChannelId,
                StatementJson = statementJson,
            });
        }
        else
        {
            existing.TeamId = teamId;
            existing.CarNumber = carNumber;
            existing.Name = definition.Name;
            existing.Message = definition.Messsage;
            existing.DisplayChannelSourceColorHex = definition.DisplayChannelSourceColorHex;
            existing.TimeAfterAckToDisplaySecs = definition.TimeAfterAckToDisplaySecs;
            existing.AlarmStatusChannelId = definition.AlarmStatusChannelId;
            existing.StatementJson = statementJson;
        }

        await db.SaveChangesAsync(ct);

        // Evict every cached set for this team — covers both car-level edits and
        // team-level edits (which would otherwise need cross-car invalidation).
        await InvalidateTeamAsync(teamId, ct);
    }

    public Task InvalidateTeamAsync(int teamId, CancellationToken ct = default) =>
        cache.RemoveByTagAsync(TeamTag(teamId), ct).AsTask();

    private static StatementDefinition DeserializeStatement(string json, ILogger logger, Guid alarmId)
    {
        if (string.IsNullOrWhiteSpace(json)) return new StatementDefinition();
        try
        {
            return JsonSerializer.Deserialize<StatementDefinition>(json) ?? new StatementDefinition();
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize StatementJson for alarm {AlarmId}", alarmId);
            return new StatementDefinition();
        }
    }

    private static string CacheKey(int teamId, string carNumber) => $"alarms:{teamId}:{carNumber}";
    private static string TeamTag(int teamId) => $"alarms-team:{teamId}";
}
