using Channels;
using Cloud.Shared;
using Cloud.Shared.Streaming;
using MessagePack;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Cloud.Tests.Streaming;

[TestClass]
public class TeamChannelPublisherTests
{
    private Mock<IConnectionMultiplexer> _mux = null!;
    private Mock<IDatabase> _db = null!;
    private TeamChannelPublisher _publisher = null!;

    private const int TeamId = 7;
    private const string TeamField = "team-7";

    [TestInitialize]
    public void Setup()
    {
        _mux = new Mock<IConnectionMultiplexer>();
        _db = new Mock<IDatabase>();

        _mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_db.Object);
        _db.Setup(d => d.StreamAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
                It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
                It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue("1-0"));

        var logger = new Mock<ILogger<TeamChannelPublisher>>();
        _publisher = new TeamChannelPublisher(_mux.Object, logger.Object);
    }

    private byte[]? SetupPayloadCapture()
    {
        byte[]? captured = null;
        _db.Setup(d => d.StreamAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
                It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
                It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, RedisValue, RedisValue?, long?, bool, long?, StreamTrimMode, CommandFlags>(
                (_, _, val, _, _, _, _, _, _) => captured = (byte[])val!)
            .ReturnsAsync(new RedisValue("1-0"));
        return captured;
    }

    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task PublishAsync_WritesTeamStreamWithCorrectFieldAndPayload()
    {
        byte[]? captured = null;
        _db.Setup(d => d.StreamAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
                It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
                It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, RedisValue, RedisValue?, long?, bool, long?, StreamTrimMode, CommandFlags>(
                (_, _, val, _, _, _, _, _, _) => captured = (byte[])val!)
            .ReturnsAsync(new RedisValue("1-0"));

        var ch1 = Guid.NewGuid();
        var ch2 = Guid.NewGuid();
        var ts = new DateTime(2026, 5, 24, 12, 0, 0, DateTimeKind.Utc);
        var values = new[]
        {
            new TeamChannelValue { ChannelId = ch1, Value = "Green", Timestamp = ts },
            new TeamChannelValue { ChannelId = ch2, Value = "Yellow", Timestamp = ts },
        };

        await _publisher.PublishAsync(TeamId, values, CancellationToken.None);

        _db.Verify(d => d.StreamAddAsync(
            It.Is<RedisKey>(k => k == Consts.TEAM_CHANNEL_VALUES_STREAM_KEY),
            It.Is<RedisValue>(f => f.ToString() == TeamField),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
            It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()), Times.Once);

        Assert.IsNotNull(captured);
        var deserialized = MessagePackSerializer.Deserialize<TeamChannelValue[]>(captured!);
        Assert.HasCount(2, deserialized);
        Assert.AreEqual(ch1, deserialized[0].ChannelId);
        Assert.AreEqual("Green", deserialized[0].Value);
        Assert.AreEqual(ch2, deserialized[1].ChannelId);
        Assert.AreEqual("Yellow", deserialized[1].Value);
    }

    [TestMethod]
    public async Task PublishAsync_EmptyInput_SkipsWrite()
    {
        await _publisher.PublishAsync(TeamId, Array.Empty<TeamChannelValue>(), CancellationToken.None);

        _db.Verify(d => d.StreamAddAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
            It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
            It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [TestMethod]
    public async Task PublishAsync_DifferentTeamIds_ProduceDifferentFieldNames()
    {
        await _publisher.PublishAsync(1, new[] { new TeamChannelValue { ChannelId = Guid.NewGuid(), Value = "x", Timestamp = DateTime.UtcNow } }, CancellationToken.None);
        await _publisher.PublishAsync(42, new[] { new TeamChannelValue { ChannelId = Guid.NewGuid(), Value = "y", Timestamp = DateTime.UtcNow } }, CancellationToken.None);

        _db.Verify(d => d.StreamAddAsync(
            It.IsAny<RedisKey>(),
            It.Is<RedisValue>(f => f.ToString() == "team-1"),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
            It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()), Times.Once);
        _db.Verify(d => d.StreamAddAsync(
            It.IsAny<RedisKey>(),
            It.Is<RedisValue>(f => f.ToString() == "team-42"),
            It.IsAny<RedisValue>(),
            It.IsAny<RedisValue?>(), It.IsAny<long?>(), It.IsAny<bool>(),
            It.IsAny<long?>(), It.IsAny<StreamTrimMode>(), It.IsAny<CommandFlags>()), Times.Once);
    }
}
