using System.Text.Json;
using Channels.Alarms;
using Cloud.Shared;
using Cloud.Shared.Alarms;
using Moq;
using StackExchange.Redis;

namespace Cloud.Tests.Shared.Alarms;

[TestClass]
public class RedisAlarmStateGatewayTests
{
    private const string CarKey = "team-1-car-77";
    private static readonly Guid AlarmId = Guid.Parse("00000000-0000-0000-0000-000000000A11");

    private Mock<IConnectionMultiplexer> _mux = null!;
    private Mock<IDatabase> _db = null!;
    private RedisAlarmStateGateway _gateway = null!;

    [TestInitialize]
    public void Setup()
    {
        _mux = new Mock<IConnectionMultiplexer>();
        _db = new Mock<IDatabase>();
        _mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_db.Object);
        _gateway = new RedisAlarmStateGateway(_mux.Object);
    }

    private void StubStringSet() =>
        _db.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(true);

    [TestMethod]
    public async Task GetAsync_NoStored_ReturnsDefaultStateWithId()
    {
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(RedisValue.Null);

        var state = await _gateway.GetAsync(CarKey, AlarmId);

        Assert.AreEqual(AlarmId, state.Id);
        Assert.IsFalse(state.IsActive);
    }

    [TestMethod]
    public async Task GetAsync_Stored_DeserializesFromRedis()
    {
        var stored = new AlarmState { Id = AlarmId, IsActive = true, IsAcknowledged = true };
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync((RedisValue)JsonSerializer.Serialize(stored));

        var state = await _gateway.GetAsync(CarKey, AlarmId);

        Assert.IsTrue(state.IsActive);
        Assert.IsTrue(state.IsAcknowledged);
    }

    [TestMethod]
    public async Task GetAsync_StoredWithEmptyId_BackfillsCallerId()
    {
        // Defensive: if a row was persisted with default(Guid), Get should restore the requested id.
        var raw = "{\"Id\":\"00000000-0000-0000-0000-000000000000\",\"IsActive\":true}";
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync((RedisValue)raw);

        var state = await _gateway.GetAsync(CarKey, AlarmId);

        Assert.AreEqual(AlarmId, state.Id);
    }

    [TestMethod]
    public async Task SetAsync_WritesAtTheExpectedKeyWithSerializedPayload()
    {
        StubStringSet();
        var expectedKey = string.Format(Consts.ALARM_STATE_KEY, CarKey, AlarmId);

        await _gateway.SetAsync(CarKey, new AlarmState { Id = AlarmId, IsActive = true });

        _db.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k.ToString() == expectedKey),
            It.Is<RedisValue>(v => v.ToString().Contains("\"IsActive\":true")),
            It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [TestMethod]
    public async Task AcknowledgeAsync_NotYetAcked_SetsBothFieldsAndPersists()
    {
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync((RedisValue)JsonSerializer.Serialize(new AlarmState { Id = AlarmId, IsActive = true }));
        StubStringSet();

        var ackTime = new DateTime(2026, 5, 24, 14, 30, 0, DateTimeKind.Utc);
        var result = await _gateway.AcknowledgeAsync(CarKey, AlarmId, ackTime);

        Assert.IsTrue(result.IsAcknowledged);
        Assert.AreEqual(ackTime, result.LastAcknowledgedTimestamp);
        _db.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.Is<RedisValue>(v => v.ToString().Contains("\"IsAcknowledged\":true")),
            It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    [TestMethod]
    public async Task AcknowledgeAsync_AlreadyAcked_IsNoOp()
    {
        var preAck = new DateTime(2026, 5, 24, 10, 0, 0, DateTimeKind.Utc);
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync((RedisValue)JsonSerializer.Serialize(new AlarmState
           {
               Id = AlarmId,
               IsActive = true,
               IsAcknowledged = true,
               LastAcknowledgedTimestamp = preAck,
           }));

        var result = await _gateway.AcknowledgeAsync(CarKey, AlarmId, DateTime.UtcNow);

        Assert.AreEqual(preAck, result.LastAcknowledgedTimestamp);
        _db.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(), It.IsAny<When>(), It.IsAny<CommandFlags>()),
            Times.Never);
    }
}
