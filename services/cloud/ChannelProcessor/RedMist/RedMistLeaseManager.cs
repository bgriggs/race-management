using Cloud.Shared.RedMist;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ChannelProcessor.RedMist;

/// <summary>
/// Per-team SETNX lease coordinator. Mediates the "single replica subscribes to RedMist per
/// team" invariant from ADR-0008. Renewal is checked-token: a replica may only renew the
/// lease key while the stored token still matches its own pod token; otherwise it has lost
/// the lease and must release its subscription.
/// </summary>
public sealed class RedMistLeaseManager
{
    /// <summary>How long an unrenewed lease is considered held.</summary>
    public static readonly TimeSpan LeaseTtl = TimeSpan.FromSeconds(60);

    /// <summary>Renewal cadence; chosen well under <see cref="LeaseTtl"/> to absorb a missed tick.</summary>
    public static readonly TimeSpan RenewalInterval = TimeSpan.FromSeconds(30);

    /// <summary>Lua script for an atomic "release iff our token still owns it" — prevents a stale
    /// caller from accidentally releasing a lease the new holder already took over.</summary>
    private const string ReleaseScript = @"
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('DEL', KEYS[1])
else
    return 0
end";

    /// <summary>Lua script for "renew iff our token still owns it" — guards against renewing
    /// a lease another holder has claimed.</summary>
    private const string RenewScript = @"
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('PEXPIRE', KEYS[1], ARGV[2])
else
    return 0
end";

    private readonly IConnectionMultiplexer redis;
    private readonly ILogger<RedMistLeaseManager> logger;

    public RedMistLeaseManager(IConnectionMultiplexer redis, ILogger<RedMistLeaseManager> logger)
    {
        this.redis = redis;
        this.logger = logger;
    }

    /// <summary>
    /// Attempts to acquire the lease for the given team. Returns <c>true</c> when the caller
    /// becomes (or remains) the lease holder, <c>false</c> when another replica holds it.
    /// </summary>
    public async Task<bool> TryAcquireAsync(int teamId, string podToken, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var key = string.Format(RedMistConsts.LEASE_KEY, teamId);
        return await db.StringSetAsync(key, podToken, LeaseTtl, when: When.NotExists);
    }

    /// <summary>
    /// Renews the TTL on the lease iff our token still holds it. Returns <c>true</c> on a
    /// successful renew; <c>false</c> when someone else now holds the lease.
    /// </summary>
    public async Task<bool> TryRenewAsync(int teamId, string podToken, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var key = string.Format(RedMistConsts.LEASE_KEY, teamId);
        var result = await db.ScriptEvaluateAsync(
            RenewScript,
            keys: [key],
            values: [podToken, (RedisValue)(long)LeaseTtl.TotalMilliseconds]);
        return (long)result == 1;
    }

    /// <summary>
    /// Releases the lease iff our token still holds it. No-op when we already lost it.
    /// </summary>
    public async Task ReleaseAsync(int teamId, string podToken, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var key = string.Format(RedMistConsts.LEASE_KEY, teamId);
        await db.ScriptEvaluateAsync(
            ReleaseScript,
            keys: [key],
            values: [podToken]);
    }
}
