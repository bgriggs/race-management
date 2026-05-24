using ChannelProcessor.Telemetry;
using Channels;
using MessagePack;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Cloud.Tests.ChannelProcessor;

[TestClass]
public class TeamChannelStreamConsumerTests
{
    private Mock<IConnectionMultiplexer> _mux = null!;
    private Mock<IDatabase> _db = null!;
    private FakeTeamChannelStateRepository _teamState = null!;
    private TeamChannelStreamConsumer _consumer = null!;

    private const int TeamId = 42;
    private const string TeamField = "team-42";

    [TestInitialize]
    public void Setup()
    {
        _mux = new Mock<IConnectionMultiplexer>();
        _db = new Mock<IDatabase>();
        _teamState = new FakeTeamChannelStateRepository();

        _mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_db.Object);

        _db.Setup(d => d.StreamCreateConsumerGroupAsync(
               It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue?>(),
               It.IsAny<bool>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(true);

        _db.Setup(d => d.StreamAcknowledgeAsync(
               It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
               It.IsAny<CommandFlags>()))
           .ReturnsAsync(1L);

        var logger = new Mock<ILogger<TeamChannelStreamConsumer>>();
        _consumer = new TeamChannelStreamConsumer(_mux.Object, _teamState, logger.Object);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _consumer.StopAsync(CancellationToken.None);
        _consumer.Dispose();
    }

    private SemaphoreSlim SetupStreamReads(StreamEntry[] firstBatch)
    {
        var batchProcessed = new SemaphoreSlim(0, 1);
        int callCount = 0;

        _db.Setup(d => d.StreamReadGroupAsync(
               It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
               It.IsAny<RedisValue?>(), It.IsAny<int?>(), It.IsAny<bool>(),
               It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(() =>
           {
               if (callCount++ == 0) return firstBatch;
               batchProcessed.Release();
               return [];
           });

        return batchProcessed;
    }

    private static StreamEntry BuildEntry(string teamField, TeamChannelValue[] values, string id = "1-0")
    {
        byte[] payload = MessagePackSerializer.Serialize(values);
        return new StreamEntry(id, [new NameValueEntry(teamField, payload)]);
    }

    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Execute_ValidEntry_CallsSetIfChangedForEachValue()
    {
        var ch1 = Guid.NewGuid();
        var ch2 = Guid.NewGuid();
        TeamChannelValue[] values =
        [
            new() { ChannelId = ch1, Value = "Green", Timestamp = DateTime.UtcNow },
            new() { ChannelId = ch2, Value = "5.0", Timestamp = DateTime.UtcNow },
        ];
        var done = SetupStreamReads([BuildEntry(TeamField, values)]);

        await _consumer.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _consumer.StopAsync(CancellationToken.None);

        Assert.IsTrue(_teamState.WasSetIfChangedCalledWith(TeamId, ch1, "Green"));
        Assert.IsTrue(_teamState.WasSetIfChangedCalledWith(TeamId, ch2, "5.0"));
        Assert.HasCount(2, _teamState.SetIfChangedCalls);
    }

    [TestMethod]
    public async Task Execute_ValidEntry_AcksAfterProcessing()
    {
        TeamChannelValue[] values = [new() { ChannelId = Guid.NewGuid(), Value = "x", Timestamp = DateTime.UtcNow }];
        var done = SetupStreamReads([BuildEntry(TeamField, values, "7-0")]);

        await _consumer.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _consumer.StopAsync(CancellationToken.None);

        _db.Verify(d => d.StreamAcknowledgeAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.Is<RedisValue>(id => id.ToString() == "7-0"),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [TestMethod]
    public async Task Execute_InvalidMessagePackPayload_StillAcksEntry()
    {
        byte[] garbage = [0xFF, 0xFE, 0x00, 0x01];
        var entry = new StreamEntry("2-0", [new NameValueEntry(TeamField, garbage)]);
        var done = SetupStreamReads([entry]);

        await _consumer.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _consumer.StopAsync(CancellationToken.None);

        Assert.IsEmpty(_teamState.SetIfChangedCalls);
        _db.Verify(d => d.StreamAcknowledgeAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [TestMethod]
    public async Task Execute_UnrecognizedFieldName_Skipped()
    {
        TeamChannelValue[] values = [new() { ChannelId = Guid.NewGuid(), Value = "x", Timestamp = DateTime.UtcNow }];
        byte[] payload = MessagePackSerializer.Serialize(values);
        var entry = new StreamEntry("3-0", [new NameValueEntry("car-team-1-car-X", payload)]);
        var done = SetupStreamReads([entry]);

        await _consumer.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _consumer.StopAsync(CancellationToken.None);

        Assert.IsEmpty(_teamState.SetIfChangedCalls);
        _db.Verify(d => d.StreamAcknowledgeAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [TestMethod]
    public async Task Execute_MultipleTeamFieldsInOneEntry_EachTeamProcessed()
    {
        var ch = Guid.NewGuid();
        TeamChannelValue[] values = [new() { ChannelId = ch, Value = "v", Timestamp = DateTime.UtcNow }];
        byte[] payload = MessagePackSerializer.Serialize(values);
        var entry = new StreamEntry("4-0",
        [
            new NameValueEntry("team-1", payload),
            new NameValueEntry("team-2", payload),
        ]);
        var done = SetupStreamReads([entry]);

        await _consumer.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _consumer.StopAsync(CancellationToken.None);

        Assert.IsTrue(_teamState.WasSetIfChangedCalledWith(1, ch, "v"));
        Assert.IsTrue(_teamState.WasSetIfChangedCalledWith(2, ch, "v"));
        Assert.HasCount(2, _teamState.SetIfChangedCalls);
    }

    [TestMethod]
    public async Task Execute_ConsumerGroupAlreadyExists_StartupContinuesNormally()
    {
        _db.Setup(d => d.StreamCreateConsumerGroupAsync(
               It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue?>(),
               It.IsAny<bool>(), It.IsAny<CommandFlags>()))
           .ThrowsAsync(new RedisServerException("BUSYGROUP Consumer Group name already exists"));
        var done = SetupStreamReads([]);

        await _consumer.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _consumer.StopAsync(CancellationToken.None);
    }
}
