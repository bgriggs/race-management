using ChannelProcessor.Telemetry;
using Channels;
using MessagePack;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Cloud.Tests.ChannelProcessor;

[TestClass]
public class TelemetryStreamConsumerTests
{
    private Mock<IConnectionMultiplexer> _mux = null!;
    private Mock<IDatabase> _db = null!;
    private FakeCarChannelStateRepository _channelState = null!;
    private TelemetryStreamConsumer _consumer = null!;

    private const string CarKey = "team-1-car-TestCar";

    [TestInitialize]
    public void Setup()
    {
        _mux = new Mock<IConnectionMultiplexer>();
        _db = new Mock<IDatabase>();
        _channelState = new FakeCarChannelStateRepository();

        _mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_db.Object);

        _db.Setup(d => d.StreamCreateConsumerGroupAsync(
               It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue?>(),
               It.IsAny<bool>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(true);

        _db.Setup(d => d.StreamAcknowledgeAsync(
               It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
               It.IsAny<CommandFlags>()))
           .ReturnsAsync(1L);

        var logger = new Mock<ILogger<TelemetryStreamConsumer>>();
        _consumer = new TelemetryStreamConsumer(_mux.Object, _channelState, logger.Object);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _consumer.StopAsync(CancellationToken.None);
        _consumer.Dispose();
    }

    /// <summary>
    /// Sets up the stream mock so the first read returns <paramref name="firstBatch"/>.
    /// Returns a <see cref="SemaphoreSlim"/> released on the second stream read (after the
    /// first batch has been fully processed and ACKed), so tests can await that moment and
    /// then call StopAsync to terminate the loop cleanly without timing dependencies.
    ///
    /// NOTE: The production code resolves to the 8-parameter overload of StreamReadGroupAsync:
    /// (key, groupName, consumerName, position?, count?, noAck, TimeSpan? claimMinIdleTime, flags).
    /// </summary>
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

    private static StreamEntry BuildEntry(string carKey, ChannelValue[] channelValues)
    {
        byte[] payload = MessagePackSerializer.Serialize(channelValues);
        return new StreamEntry("1-0", [new NameValueEntry(carKey, payload)]);
    }

    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Execute_ValidStreamEntry_CallsSetIfChangedForEachChannelValue()
    {
        ChannelValue[] channelValues =
        [
            new() { SessionIndex = 0, Value = "100", Timestamp = DateTime.UtcNow },
            new() { SessionIndex = 1, Value = "200", Timestamp = DateTime.UtcNow },
        ];
        var done = SetupStreamReads([BuildEntry(CarKey, channelValues)]);

        await _consumer.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _consumer.StopAsync(CancellationToken.None);

        Assert.IsTrue(_channelState.WasSetIfChangedCalledWith(CarKey, 0, "100"));
        Assert.IsTrue(_channelState.WasSetIfChangedCalledWith(CarKey, 1, "200"));
        Assert.HasCount(2, _channelState.SetIfChangedCalls);
    }

    [TestMethod]
    public async Task Execute_ValidStreamEntry_AcksEntryAfterProcessing()
    {
        ChannelValue[] channelValues = [new() { SessionIndex = 0, Value = "1", Timestamp = DateTime.UtcNow }];
        var done = SetupStreamReads([BuildEntry(CarKey, channelValues)]);

        await _consumer.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _consumer.StopAsync(CancellationToken.None);

        _db.Verify(d => d.StreamAcknowledgeAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.Is<RedisValue>(id => id.ToString() == "1-0"),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [TestMethod]
    public async Task Execute_EmptyStream_MakesNoRepositoryCalls()
    {
        var done = SetupStreamReads([]);

        await _consumer.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _consumer.StopAsync(CancellationToken.None);

        Assert.IsEmpty(_channelState.SetIfChangedCalls);
    }

    [TestMethod]
    public async Task Execute_InvalidMessagePackPayload_StillAcksEntry()
    {
        byte[] garbage = [0xFF, 0xFE, 0x00, 0x01];
        var entry = new StreamEntry("2-0", [new NameValueEntry(CarKey, garbage)]);
        var done = SetupStreamReads([entry]);

        await _consumer.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _consumer.StopAsync(CancellationToken.None);

        Assert.IsEmpty(_channelState.SetIfChangedCalls);
        _db.Verify(d => d.StreamAcknowledgeAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()), Times.Once);
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
        // reaching here without exception confirms BUSYGROUP was swallowed
    }

    [TestMethod]
    public async Task Execute_EntryWithMultipleCarFields_ProcessesEachCarSeparately()
    {
        const string carKey2 = "team-1-car-Car2";
        ChannelValue[] values = [new() { SessionIndex = 0, Value = "5", Timestamp = DateTime.UtcNow }];
        byte[] payload = MessagePackSerializer.Serialize(values);
        var entry = new StreamEntry("3-0",
        [
            new NameValueEntry(CarKey, payload),
            new NameValueEntry(carKey2, payload),
        ]);
        var done = SetupStreamReads([entry]);

        await _consumer.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _consumer.StopAsync(CancellationToken.None);

        Assert.IsTrue(_channelState.WasSetIfChangedCalledWith(CarKey, 0, "5"));
        Assert.IsTrue(_channelState.WasSetIfChangedCalledWith(carKey2, 0, "5"));
        Assert.HasCount(2, _channelState.SetIfChangedCalls);
    }

    [TestMethod]
    public async Task Execute_RepeatedSameValue_RepositorySeesEachCall()
    {
        // The consumer calls SetIfChanged unconditionally; change detection is the repo's job
        ChannelValue[] values = [new() { SessionIndex = 0, Value = "42", Timestamp = DateTime.UtcNow }];
        var entry = BuildEntry(CarKey, values);

        int callCount = 0;
        _db.Setup(d => d.StreamReadGroupAsync(
               It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
               It.IsAny<RedisValue?>(), It.IsAny<int?>(), It.IsAny<bool>(),
               It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(() => callCount++ < 2 ? (StreamEntry[])[entry] : []);

        var done = new SemaphoreSlim(0, 1);
        _db.Setup(d => d.StreamAcknowledgeAsync(
               It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
               It.IsAny<CommandFlags>()))
           .Callback(() => { if (callCount >= 2) done.Release(); })
           .ReturnsAsync(1L);

        await _consumer.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _consumer.StopAsync(CancellationToken.None);

        // Two batches processed; fake's change-detection means only first is "changed"
        Assert.IsGreaterThanOrEqualTo(2, _channelState.SetIfChangedCalls.Count);
    }
}
