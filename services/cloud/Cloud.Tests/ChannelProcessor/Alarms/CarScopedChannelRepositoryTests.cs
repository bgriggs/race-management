using ChannelProcessor.Alarms.Channel;
using Channels;
using Cloud.Shared;
using Cloud.Shared.Streaming;
using Cloud.Shared.Telemetry;
using MessagePack;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Cloud.Tests.ChannelProcessor.Alarms;

[TestClass]
public class CarScopedChannelRepositoryTests
{
    private const int TeamId = 7;
    private const string CarNumber = "42";
    private const string CarKey = "team-7-car-42";

    private Mock<IConnectionMultiplexer> _mux = null!;
    private Mock<IDatabase> _db = null!;
    private Mock<ICarChannelPublisher> _carPub = null!;
    private Mock<ITeamChannelPublisher> _teamPub = null!;
    private TimeProvider _time = null!;
    private ILogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _mux = new Mock<IConnectionMultiplexer>();
        _db = new Mock<IDatabase>();
        _mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_db.Object);
        _carPub = new Mock<ICarChannelPublisher>();
        _teamPub = new Mock<ITeamChannelPublisher>();
        _time = TimeProvider.System;
        _logger = new Mock<ILogger>().Object;
    }

    private static ChannelDefinition Def(Guid id, ChannelScope scope) =>
        new() { Id = id, Scope = scope };

    private CarScopedChannelRepository Create(
        Dictionary<Guid, ChannelDefinition> defs,
        Dictionary<Guid, ushort>? sessionIndex = null) =>
        new(TeamId, CarNumber, CarKey, defs, sessionIndex ?? new Dictionary<Guid, ushort>(),
            _mux.Object, _carPub.Object, _teamPub.Object, _time, _logger, CancellationToken.None);

    private static byte[] SnapshotBytes(string value) =>
        MessagePackSerializer.Serialize(new ChannelValueSnapshot { Value = value, Timestamp = DateTime.UtcNow });

    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task GetChannelValue_PerCar_ReadsCarHashViaSessionIndex()
    {
        var channelId = Guid.NewGuid();
        var defs = new Dictionary<Guid, ChannelDefinition> { [channelId] = Def(channelId, ChannelScope.PerCar) };
        var sess = new Dictionary<Guid, ushort> { [channelId] = 12 };

        _db.Setup(d => d.HashGetAsync(
                It.Is<RedisKey>(k => k.ToString() == string.Format(Consts.CAR_CHANNEL_STATE_KEY, CarKey)),
                It.Is<RedisValue>(f => f.ToString() == "12"),
                It.IsAny<CommandFlags>()))
           .ReturnsAsync((RedisValue)SnapshotBytes("123"));

        var repo = Create(defs, sess);
        var value = await repo.GetChannelValueAsync(channelId);

        Assert.AreEqual("123", value.Value);
    }

    [TestMethod]
    public async Task GetChannelValue_PerTeam_ReadsTeamHashByChannelId()
    {
        var channelId = Guid.NewGuid();
        var defs = new Dictionary<Guid, ChannelDefinition> { [channelId] = Def(channelId, ChannelScope.PerTeam) };

        _db.Setup(d => d.HashGetAsync(
                It.Is<RedisKey>(k => k.ToString() == string.Format(Consts.TEAM_CHANNEL_STATE_KEY, TeamId)),
                It.Is<RedisValue>(f => f.ToString() == channelId.ToString()),
                It.IsAny<CommandFlags>()))
           .ReturnsAsync((RedisValue)SnapshotBytes("green"));

        var repo = Create(defs);
        var value = await repo.GetChannelValueAsync(channelId);

        Assert.AreEqual("green", value.Value);
    }

    [TestMethod]
    public async Task GetChannelValue_UnknownChannel_ReturnsEmpty()
    {
        var repo = Create(new Dictionary<Guid, ChannelDefinition>());
        var value = await repo.GetChannelValueAsync(Guid.NewGuid());

        Assert.AreEqual(string.Empty, value.Value);
        _db.Verify(d => d.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [TestMethod]
    public async Task GetChannelValue_PerCar_MissingSessionIndex_ReturnsEmpty()
    {
        var channelId = Guid.NewGuid();
        var defs = new Dictionary<Guid, ChannelDefinition> { [channelId] = Def(channelId, ChannelScope.PerCar) };
        // No entry in the session map for this channel.

        var repo = Create(defs);
        var value = await repo.GetChannelValueAsync(channelId);

        Assert.AreEqual(string.Empty, value.Value);
        _db.Verify(d => d.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [TestMethod]
    public async Task SetChannelValue_PerCar_RoutesThroughCarPublisher()
    {
        var channelId = Guid.NewGuid();
        var defs = new Dictionary<Guid, ChannelDefinition> { [channelId] = Def(channelId, ChannelScope.PerCar) };
        var repo = Create(defs);

        await repo.SetChannelValueAsync(channelId, new ChannelValue { Value = "1" });

        _carPub.Verify(p => p.PublishAsync(
            TeamId,
            CarNumber,
            It.Is<IReadOnlyList<PublishedChannelValue>>(list => list.Count == 1 && list[0].ChannelId == channelId && list[0].Value == "1"),
            It.IsAny<CancellationToken>()), Times.Once);
        _teamPub.Verify(p => p.PublishAsync(It.IsAny<int>(), It.IsAny<TeamChannelValue[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SetChannelValue_PerTeam_RoutesThroughTeamPublisher()
    {
        var channelId = Guid.NewGuid();
        var defs = new Dictionary<Guid, ChannelDefinition> { [channelId] = Def(channelId, ChannelScope.PerTeam) };
        var repo = Create(defs);

        await repo.SetChannelValueAsync(channelId, new ChannelValue { Value = "0" });

        _teamPub.Verify(p => p.PublishAsync(
            TeamId,
            It.Is<TeamChannelValue[]>(arr => arr.Length == 1 && arr[0].ChannelId == channelId && arr[0].Value == "0"),
            It.IsAny<CancellationToken>()), Times.Once);
        _carPub.Verify(p => p.PublishAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<PublishedChannelValue>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task SetChannelValue_UnknownChannel_DoesNotPublish()
    {
        var repo = Create(new Dictionary<Guid, ChannelDefinition>());

        await repo.SetChannelValueAsync(Guid.NewGuid(), new ChannelValue { Value = "1" });

        _carPub.Verify(p => p.PublishAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<PublishedChannelValue>>(), It.IsAny<CancellationToken>()), Times.Never);
        _teamPub.Verify(p => p.PublishAsync(It.IsAny<int>(), It.IsAny<TeamChannelValue[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
