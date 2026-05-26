using System.Text.Json;
using Cloud.Shared.RedMist;
using StackExchange.Redis;

namespace ChannelProcessor.RedMist;

/// <summary>
/// Persists the per-team RedMist connection status to Redis. Read on demand by WebApi's
/// <c>GET /v1/redmist/connection-status</c> endpoint. Keyed by teamId so a single replica
/// can write the status it knows about without leader-election on the read side.
/// </summary>
public sealed class RedMistStatusWriter
{
    private static readonly TimeSpan StatusTtl = TimeSpan.FromMinutes(15);

    private readonly IConnectionMultiplexer redis;
    private readonly TimeProvider time;

    public RedMistStatusWriter(IConnectionMultiplexer redis, TimeProvider time)
    {
        this.redis = redis;
        this.time = time;
    }

    public async Task WriteAsync(int teamId, RedMistConnectionState state, int? eventId, string? detail, CancellationToken ct)
    {
        var dto = new RedMistConnectionStatusDto
        {
            State = state,
            LastChangeAtUtc = time.GetUtcNow().UtcDateTime,
            EventId = eventId,
            Detail = detail,
        };
        var json = JsonSerializer.Serialize(dto);
        var db = redis.GetDatabase();
        await db.StringSetAsync(string.Format(RedMistConsts.STATUS_KEY, teamId), json, StatusTtl);
    }

    public async Task<RedMistConnectionStatusDto?> ReadAsync(int teamId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var json = await db.StringGetAsync(string.Format(RedMistConsts.STATUS_KEY, teamId));
        if (json.IsNullOrEmpty) return null;
        return JsonSerializer.Deserialize<RedMistConnectionStatusDto>(json.ToString());
    }
}
