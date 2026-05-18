using ChannelProcessor.Telemetry;
using Cloud.Shared.Telemetry;
using MessagePack;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Cloud.Tests.ChannelProcessor;

// =============================================================================
// Contract tests — run against the fake to pin the expected behaviour of
// ICarChannelStateRepository.  Any real implementation must satisfy these.
// =============================================================================

[TestClass]
public class CarChannelStateRepositoryContractTests
{
    private FakeCarChannelStateRepository _repo = null!;

    private const string CarKey = "team-1-car-TestCar";

    [TestInitialize]
    public void Setup() => _repo = new FakeCarChannelStateRepository();

    [TestMethod]
    public async Task SetIfChanged_FirstValue_ReturnsTrue()
    {
        var result = await _repo.SetIfChangedAsync(CarKey, 0, Snap("42"));
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task SetIfChanged_SameValueAgain_ReturnsFalse()
    {
        await _repo.SetIfChangedAsync(CarKey, 0, Snap("42"));
        var result = await _repo.SetIfChangedAsync(CarKey, 0, Snap("42"));
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task SetIfChanged_ValueChanges_ReturnsTrue()
    {
        await _repo.SetIfChangedAsync(CarKey, 0, Snap("10"));
        var result = await _repo.SetIfChangedAsync(CarKey, 0, Snap("99"));
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task SetIfChanged_DifferentSessionIndexes_TrackedIndependently()
    {
        await _repo.SetIfChangedAsync(CarKey, 0, Snap("1"));
        await _repo.SetIfChangedAsync(CarKey, 1, Snap("1"));

        // same value, same channel → no change
        Assert.IsFalse(await _repo.SetIfChangedAsync(CarKey, 0, Snap("1")));
        // same value, different channel that hasn't seen this value → still false (already set)
        Assert.IsFalse(await _repo.SetIfChangedAsync(CarKey, 1, Snap("1")));

        // change only channel 1
        Assert.IsTrue(await _repo.SetIfChangedAsync(CarKey, 1, Snap("2")));
        Assert.IsFalse(await _repo.SetIfChangedAsync(CarKey, 0, Snap("1")));
    }

    [TestMethod]
    public async Task SetIfChanged_DifferentCarKeys_TrackedIndependently()
    {
        await _repo.SetIfChangedAsync("car-A", 0, Snap("5"));
        // Same value on car-B is still a first write → changed
        Assert.IsTrue(await _repo.SetIfChangedAsync("car-B", 0, Snap("5")));
    }

    [TestMethod]
    public async Task GetAll_ReturnsOnlyChannelsForRequestedCar()
    {
        await _repo.SetIfChangedAsync("car-A", 0, Snap("1"));
        await _repo.SetIfChangedAsync("car-A", 1, Snap("2"));
        await _repo.SetIfChangedAsync("car-B", 0, Snap("3"));

        var result = await _repo.GetAllAsync("car-A");

        Assert.HasCount(2, result);
        Assert.AreEqual("1", result[0].Value);
        Assert.AreEqual("2", result[1].Value);
    }

    [TestMethod]
    public async Task GetAll_NoChannels_ReturnsEmptyDictionary()
    {
        var result = await _repo.GetAllAsync(CarKey);
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public async Task Clear_RemovesAllChannelsForCar_LeavesOtherCarsIntact()
    {
        await _repo.SetIfChangedAsync("car-A", 0, Snap("1"));
        await _repo.SetIfChangedAsync("car-B", 0, Snap("2"));

        await _repo.ClearAsync("car-A");

        Assert.IsEmpty(await _repo.GetAllAsync("car-A"));
        Assert.HasCount(1, await _repo.GetAllAsync("car-B"));
    }

    [TestMethod]
    public async Task Clear_AfterClear_NewValueIsSeenAsChanged()
    {
        await _repo.SetIfChangedAsync(CarKey, 0, Snap("42"));
        await _repo.ClearAsync(CarKey);

        // After clear the channel is gone — the same value is a new write
        Assert.IsTrue(await _repo.SetIfChangedAsync(CarKey, 0, Snap("42")));
    }

    // -------------------------------------------------------------------------
    private static ChannelValueSnapshot Snap(string value) =>
        new() { Value = value, Timestamp = DateTime.UtcNow };
}


// =============================================================================
// Redis wiring tests — verify that CarChannelStateRepository calls the right
// Redis operations.  Moq is used only at the Redis boundary (IDatabase), not
// for any of our own interfaces.
// =============================================================================

[TestClass]
public class CarChannelStateRepositoryRedisTests
{
    private Mock<IConnectionMultiplexer> _mux = null!;
    private Mock<IDatabase> _db = null!;
    private CarChannelStateRepository _repo = null!;

    private const string CarKey = "team-1-car-TestCar";

    [TestInitialize]
    public void Setup()
    {
        _mux = new Mock<IConnectionMultiplexer>();
        _db = new Mock<IDatabase>();
        _mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_db.Object);
        _db.Setup(d => d.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(true);
        _db.Setup(d => d.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(0L);

        _repo = new CarChannelStateRepository(_mux.Object, new Mock<ILogger<CarChannelStateRepository>>().Object);
    }

    [TestMethod]
    public async Task SetIfChanged_NoExistingEntry_WritesHashAndPublishes()
    {
        _db.Setup(d => d.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(RedisValue.Null);

        await _repo.SetIfChangedAsync(CarKey, 3, new ChannelValueSnapshot { Value = "77", Timestamp = DateTime.UtcNow });

        _db.Verify(d => d.HashSetAsync(It.IsAny<RedisKey>(), It.Is<RedisValue>(f => f.ToString() == "3"),
            It.IsAny<RedisValue>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
        _db.Verify(d => d.PublishAsync(
            It.Is<RedisChannel>(c => c.ToString().Contains(CarKey)),
            It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [TestMethod]
    public async Task SetIfChanged_SameValueAlreadyStored_NoWriteOrPublish()
    {
        var existing = new ChannelValueSnapshot { Value = "42", Timestamp = DateTime.UtcNow };
        RedisValue stored = MessagePackSerializer.Serialize(existing);
        _db.Setup(d => d.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(stored);

        await _repo.SetIfChangedAsync(CarKey, 0, new ChannelValueSnapshot { Value = "42", Timestamp = DateTime.UtcNow });

        _db.Verify(d => d.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Never);
        _db.Verify(d => d.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [TestMethod]
    public async Task SetIfChanged_PublishPayload_ContainsCorrectSessionIndex()
    {
        _db.Setup(d => d.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(RedisValue.Null);

        RedisValue capturedPayload = default;
        _db.Setup(d => d.PublishAsync(It.IsAny<RedisChannel>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
           .Callback<RedisChannel, RedisValue, CommandFlags>((_, v, _) => capturedPayload = v)
           .ReturnsAsync(1L);

        await _repo.SetIfChangedAsync(CarKey, 7, new ChannelValueSnapshot { Value = "5", Timestamp = DateTime.UtcNow });

        var notification = MessagePackSerializer.Deserialize<ChannelChangeNotification>((byte[])capturedPayload!);
        Assert.AreEqual(7, notification.SessionIndex);
        Assert.AreEqual("5", notification.Value);
    }

    [TestMethod]
    public async Task Clear_KeyExists_DeletesCorrectHashKey()
    {
        _db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(true);

        await _repo.ClearAsync(CarKey);

        _db.Verify(d => d.KeyDeleteAsync(
            It.Is<RedisKey>(k => k.ToString().Contains(CarKey)),
            It.IsAny<CommandFlags>()), Times.Once);
    }
}
