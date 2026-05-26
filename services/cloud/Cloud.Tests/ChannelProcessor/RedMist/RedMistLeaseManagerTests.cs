using global::ChannelProcessor.RedMist;
using Cloud.Shared.RedMist;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Cloud.Tests.ChannelProcessor.RedMist;

/// <summary>
/// Unit tests for the per-team SETNX lease primitives. The release and renew paths run Lua
/// scripts that must atomically check the caller's <c>podToken</c> against the stored
/// value; if the test mocks ever allow either to succeed under a mismatched token, two
/// replicas would subscribe to the same RedMist event (the failure mode ADR-0008 is
/// designed to prevent).
/// </summary>
[TestClass]
public class RedMistLeaseManagerTests
{
    private const int TeamId = 42;
    private const string PodToken = "pod-A-abc";

    private Mock<IConnectionMultiplexer> _mux = null!;
    private Mock<IDatabase> _db = null!;
    private RedMistLeaseManager _leases = null!;

    [TestInitialize]
    public void Setup()
    {
        _mux = new Mock<IConnectionMultiplexer>();
        _db = new Mock<IDatabase>();
        _mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_db.Object);
        _leases = new RedMistLeaseManager(_mux.Object, NullLogger<RedMistLeaseManager>.Instance);
    }

    [TestMethod]
    public void LeaseTtl_IsTwiceTheRenewalInterval_SoMissedRenewIsTolerable()
    {
        // ADR-0008 wants a missed tick to be safe. 30s renewal × 2 ≤ 60s TTL is the
        // smallest factor of safety; flipping this contract should require a deliberate
        // ADR amendment, hence the test.
        Assert.IsTrue(RedMistLeaseManager.LeaseTtl >= RedMistLeaseManager.RenewalInterval * 2,
            $"LeaseTtl ({RedMistLeaseManager.LeaseTtl}) must be at least 2× RenewalInterval ({RedMistLeaseManager.RenewalInterval}).");
    }

    // ---- TryAcquireAsync ----

    [TestMethod]
    public async Task TryAcquire_UsesCorrectKey_AndSetsTokenWithNotExists()
    {
        var expectedKey = string.Format(RedMistConsts.LEASE_KEY, TeamId);

        RedisKey? capturedKey = null;
        RedisValue? capturedValue = null;
        TimeSpan? capturedTtl = null;
        When? capturedWhen = null;
        // The SUT uses the 4-arg overload (RedisKey, RedisValue, TimeSpan?, When).
        _db.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<When>()))
           .Callback((RedisKey k, RedisValue v, TimeSpan? ttl, When when) =>
           {
               capturedKey = k;
               capturedValue = v;
               capturedTtl = ttl;
               capturedWhen = when;
           })
           .ReturnsAsync(true);

        var acquired = await _leases.TryAcquireAsync(TeamId, PodToken, default);

        Assert.IsTrue(acquired);
        Assert.AreEqual(expectedKey, (string?)capturedKey);
        Assert.AreEqual((RedisValue)PodToken, capturedValue);
        Assert.AreEqual(RedMistLeaseManager.LeaseTtl, capturedTtl);
        Assert.AreEqual(When.NotExists, capturedWhen);
    }

    [TestMethod]
    public async Task TryAcquire_ReturnsFalse_WhenLeaseAlreadyHeld()
    {
        _db.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<When>()))
           .ReturnsAsync(false);

        var acquired = await _leases.TryAcquireAsync(TeamId, PodToken, default);

        Assert.IsFalse(acquired);
    }

    // ---- TryRenewAsync ----

    [TestMethod]
    public async Task TryRenew_ReturnsTrue_WhenLuaReportsOwnership()
    {
        StubScript(returns: 1L);

        var ok = await _leases.TryRenewAsync(TeamId, PodToken, default);

        Assert.IsTrue(ok);
    }

    [TestMethod]
    public async Task TryRenew_ReturnsFalse_WhenAnotherReplicaOwnsTheLease()
    {
        // The Lua script returns 0 when GET returns a different token; if we ever returned
        // success here we'd renew someone else's lease.
        StubScript(returns: 0L);

        var ok = await _leases.TryRenewAsync(TeamId, PodToken, default);

        Assert.IsFalse(ok);
    }

    [TestMethod]
    public async Task TryRenew_SendsPodTokenAndTtlMillis()
    {
        RedisValue[]? capturedArgs = null;
        _db.Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                It.IsAny<CommandFlags>()))
           .Callback((string _, RedisKey[]? _, RedisValue[]? values, CommandFlags _) => capturedArgs = values)
           .ReturnsAsync(RedisResult.Create((RedisValue)1L));

        await _leases.TryRenewAsync(TeamId, PodToken, default);

        Assert.IsNotNull(capturedArgs);
        Assert.HasCount(2, capturedArgs);
        Assert.AreEqual((RedisValue)PodToken, capturedArgs![0]);
        Assert.AreEqual((RedisValue)(long)RedMistLeaseManager.LeaseTtl.TotalMilliseconds, capturedArgs[1]);
    }

    // ---- ReleaseAsync ----

    [TestMethod]
    public async Task Release_RunsScript_AgainstExpectedKeyAndToken()
    {
        var expectedKey = string.Format(RedMistConsts.LEASE_KEY, TeamId);
        RedisKey[]? capturedKeys = null;
        RedisValue[]? capturedArgs = null;
        _db.Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                It.IsAny<CommandFlags>()))
           .Callback((string _, RedisKey[]? keys, RedisValue[]? values, CommandFlags _) =>
           {
               capturedKeys = keys;
               capturedArgs = values;
           })
           .ReturnsAsync(RedisResult.Create((RedisValue)1L));

        await _leases.ReleaseAsync(TeamId, PodToken, default);

        Assert.IsNotNull(capturedKeys);
        Assert.IsNotNull(capturedArgs);
        Assert.HasCount(1, capturedKeys);
        Assert.AreEqual(expectedKey, (string?)capturedKeys![0]);
        Assert.HasCount(1, capturedArgs);
        Assert.AreEqual((RedisValue)PodToken, capturedArgs![0]);
    }

    [TestMethod]
    public async Task Release_DoesNotThrow_WhenScriptIndicatesNotOwner()
    {
        // The Lua script returns 0 when our token no longer holds the lease — caller's
        // detach path must complete cleanly. Surface as no-throw.
        StubScript(returns: 0L);

        await _leases.ReleaseAsync(TeamId, PodToken, default);
    }

    private void StubScript(long returns) =>
        _db.Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                It.IsAny<CommandFlags>()))
           .ReturnsAsync(RedisResult.Create((RedisValue)returns));
}
