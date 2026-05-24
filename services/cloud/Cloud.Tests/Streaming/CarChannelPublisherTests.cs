using Channels;
using Cloud.Shared;
using Cloud.Shared.Streaming;
using MessagePack;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Cloud.Tests.Streaming;

[TestClass]
public class CarChannelPublisherTests
{
    private Mock<IConnectionMultiplexer> _mux = null!;
    private Mock<IDatabase> _db = null!;
    private Mock<ICarChannelDefinitionResolver> _resolver = null!;
    private CarChannelPublisher _publisher = null!;

    private const int TeamId = 1;
    private const string Car = "Car-77";
    private const string CarKey = "team-1-car-Car-77";

    [TestInitialize]
    public void Setup()
    {
        _mux = new Mock<IConnectionMultiplexer>();
        _db = new Mock<IDatabase>();
        _resolver = new Mock<ICarChannelDefinitionResolver>();

        _mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_db.Object);
        _db.Setup(d => d.StreamAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
                It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
                It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("1-0"));

        var logger = new Mock<ILogger<CarChannelPublisher>>();
        _publisher = new CarChannelPublisher(_mux.Object, _resolver.Object, logger.Object);
    }

    private void SetupChannelIdMap(IReadOnlyDictionary<Guid, ushort>? map)
    {
        _resolver.Setup(r => r.GetChannelIdMapAsync(CarKey, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(map);
    }

    /// <summary>
    /// Captures the payload bytes from the first StreamAddAsync invocation so the test
    /// can deserialize and inspect the actual <see cref="ChannelValue"/>[] that was
    /// written.
    /// </summary>
    private byte[]? CaptureStreamAddPayload()
    {
        byte[]? captured = null;
        _db.Setup(d => d.StreamAddAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.Is<RedisValue>(v => v.HasValue && ((byte[])v!).Length > 0),
                It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
                It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, RedisValue, RedisValue?, long?, bool, long?, StreamTrimMode, CommandFlags>(
                (_, _, val, _, _, _, _, _, _) => captured = (byte[])val!)
            .ReturnsAsync(new RedisValue("1-0"));
        return captured ?? null!;
    }

    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task PublishAsync_ResolvesChannelIdsToSessionIndexes_AndWritesStream()
    {
        var channelA = Guid.NewGuid();
        var channelB = Guid.NewGuid();
        SetupChannelIdMap(new Dictionary<Guid, ushort> { [channelA] = 3, [channelB] = 11 });

        byte[]? captured = null;
        _db.Setup(d => d.StreamAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
                It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
                It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, RedisValue, RedisValue?, long?, bool, long?, StreamTrimMode, CommandFlags>(
                (_, _, val, _, _, _, _, _, _) => captured = (byte[])val!)
            .ReturnsAsync(new RedisValue("1-0"));

        var ts = new DateTime(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);
        var count = await _publisher.PublishAsync(TeamId, Car, new[]
        {
            new PublishedChannelValue(channelA, "100", ts),
            new PublishedChannelValue(channelB, "200", ts),
        }, CancellationToken.None);

        Assert.AreEqual(2, count);
        Assert.IsNotNull(captured);

        var values = MessagePackSerializer.Deserialize<ChannelValue[]>(captured);
        Assert.HasCount(2, values);
        Assert.AreEqual<ushort>(3, values[0].SessionIndex);
        Assert.AreEqual("100", values[0].Value);
        Assert.AreEqual<ushort>(11, values[1].SessionIndex);
        Assert.AreEqual("200", values[1].Value);

        _db.Verify(d => d.StreamAddAsync(
            It.Is<RedisKey>(k => k == Consts.CAR_CHANNEL_VALUES_STREAM_KEY),
            It.Is<RedisValue>(f => f.ToString() == CarKey),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
            It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [TestMethod]
    public async Task PublishAsync_NoActiveConfiguration_ReturnsZeroAndSkipsWrite()
    {
        SetupChannelIdMap(null);

        var count = await _publisher.PublishAsync(TeamId, Car, new[]
        {
            new PublishedChannelValue(Guid.NewGuid(), "v", DateTime.UtcNow),
        }, CancellationToken.None);

        Assert.AreEqual(0, count);
        _db.Verify(d => d.StreamAddAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
            It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
            It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [TestMethod]
    public async Task PublishAsync_ChannelMissingFromConfig_OtherValuesStillPublished()
    {
        var known = Guid.NewGuid();
        var unknown = Guid.NewGuid();
        SetupChannelIdMap(new Dictionary<Guid, ushort> { [known] = 5 });

        byte[]? captured = null;
        _db.Setup(d => d.StreamAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
                It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
                It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, RedisValue, RedisValue?, long?, bool, long?, StreamTrimMode, CommandFlags>(
                (_, _, val, _, _, _, _, _, _) => captured = (byte[])val!)
            .ReturnsAsync(new RedisValue("1-0"));

        var count = await _publisher.PublishAsync(TeamId, Car, new[]
        {
            new PublishedChannelValue(known, "k", DateTime.UtcNow),
            new PublishedChannelValue(unknown, "u", DateTime.UtcNow),
        }, CancellationToken.None);

        Assert.AreEqual(1, count);
        var values = MessagePackSerializer.Deserialize<ChannelValue[]>(captured!);
        Assert.HasCount(1, values);
        Assert.AreEqual<ushort>(5, values[0].SessionIndex);
        Assert.AreEqual("k", values[0].Value);
    }

    [TestMethod]
    public async Task PublishAsync_AllChannelsMissingFromConfig_ReturnsZeroAndSkipsWrite()
    {
        SetupChannelIdMap(new Dictionary<Guid, ushort>());

        var count = await _publisher.PublishAsync(TeamId, Car, new[]
        {
            new PublishedChannelValue(Guid.NewGuid(), "v", DateTime.UtcNow),
        }, CancellationToken.None);

        Assert.AreEqual(0, count);
        _db.Verify(d => d.StreamAddAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
            It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
            It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [TestMethod]
    public async Task PublishAsync_EmptyInput_ReturnsZeroAndSkipsResolverAndWrite()
    {
        var count = await _publisher.PublishAsync(TeamId, Car, Array.Empty<PublishedChannelValue>(), CancellationToken.None);

        Assert.AreEqual(0, count);
        _resolver.Verify(r => r.GetChannelIdMapAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _db.Verify(d => d.StreamAddAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
            It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
            It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [TestMethod]
    public async Task PublishAsync_PreservesTimestamps()
    {
        var channelId = Guid.NewGuid();
        SetupChannelIdMap(new Dictionary<Guid, ushort> { [channelId] = 0 });

        byte[]? captured = null;
        _db.Setup(d => d.StreamAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
                It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
                It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, RedisValue, RedisValue?, long?, bool, long?, StreamTrimMode, CommandFlags>(
                (_, _, val, _, _, _, _, _, _) => captured = (byte[])val!)
            .ReturnsAsync(new RedisValue("1-0"));

        // Backdated timestamp — engineer correcting a missed refuel entry.
        var backdated = new DateTime(2025, 8, 15, 14, 30, 0, DateTimeKind.Utc);
        await _publisher.PublishAsync(TeamId, Car, new[]
        {
            new PublishedChannelValue(channelId, "3.5", backdated),
        }, CancellationToken.None);

        var values = MessagePackSerializer.Deserialize<ChannelValue[]>(captured!);
        Assert.AreEqual(backdated, values[0].Timestamp);
    }
}
