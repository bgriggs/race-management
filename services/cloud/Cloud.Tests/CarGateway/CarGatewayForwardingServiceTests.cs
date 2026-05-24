using CarGateway.Forwarding;
using Channels;
using Cloud.Shared;
using Cloud.Shared.Hubs;
using Cloud.Shared.Streaming;
using MessagePack;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Cloud.Tests.CarGateway;

[TestClass]
public class CarGatewayForwardingServiceTests
{
    private Mock<IConnectionMultiplexer> _mux = null!;
    private Mock<IDatabase> _db = null!;
    private Mock<IHubContext<CarHub>> _hub = null!;
    private Mock<IHubClients> _hubClients = null!;
    private Mock<ISingleClientProxy> _clientProxy = null!;
    private Mock<ICarChannelDefinitionResolver> _resolver = null!;
    private CarGatewayForwardingService _service = null!;

    private const string CarKey = "team-1-car-TestCar";
    private const string ConnectionId = "conn-abc-123";

    [TestInitialize]
    public void Setup()
    {
        _mux = new Mock<IConnectionMultiplexer>();
        _db = new Mock<IDatabase>();
        _hub = new Mock<IHubContext<CarHub>>();
        _hubClients = new Mock<IHubClients>();
        _clientProxy = new Mock<ISingleClientProxy>();
        _resolver = new Mock<ICarChannelDefinitionResolver>();

        _mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_db.Object);

        _db.Setup(d => d.StreamCreateConsumerGroupAsync(
               It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue?>(),
               It.IsAny<bool>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(true);

        _db.Setup(d => d.StreamAcknowledgeAsync(
               It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
               It.IsAny<CommandFlags>()))
           .ReturnsAsync(1L);

        _hub.Setup(h => h.Clients).Returns(_hubClients.Object);
        _hubClients.Setup(c => c.Client(It.IsAny<string>())).Returns(_clientProxy.Object);

        var logger = new Mock<ILogger<CarGatewayForwardingService>>();
        _service = new CarGatewayForwardingService(_mux.Object, _hub.Object, _resolver.Object, logger.Object);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        await _service.StopAsync(CancellationToken.None);
        _service.Dispose();
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

    private static StreamEntry BuildEntry(string carKey, ChannelValue[] channelValues, string id = "1-0")
    {
        byte[] payload = MessagePackSerializer.Serialize(channelValues);
        return new StreamEntry(id, [new NameValueEntry(carKey, payload)]);
    }

    private void SetupConnectionId(string carKey, string? connectionId)
    {
        var key = string.Format(Consts.CAR_CONNECTION_BY_CAR, carKey);
        _db.Setup(d => d.StringGetAsync(
               It.Is<RedisKey>(k => k == key), It.IsAny<CommandFlags>()))
           .ReturnsAsync(connectionId is null ? RedisValue.Null : new RedisValue(connectionId));
    }

    private void SetupSessionIndexMap(string carKey, IReadOnlyDictionary<ushort, ChannelDefinition>? map)
    {
        _resolver.Setup(r => r.GetSessionIndexMapAsync(carKey, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(map);
    }

    // -------------------------------------------------------------------------

    [TestMethod]
    public async Task Execute_CloudToCarValue_ForwardedToCarConnection()
    {
        var def = new ChannelDefinition { Distribution = ChannelDistribution.CloudToCar };
        SetupSessionIndexMap(CarKey, new Dictionary<ushort, ChannelDefinition> { [5] = def });
        SetupConnectionId(CarKey, ConnectionId);

        ChannelValue[] values = [new() { SessionIndex = 5, Value = "42", Timestamp = DateTime.UtcNow }];
        var done = SetupStreamReads([BuildEntry(CarKey, values)]);

        await _service.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _service.StopAsync(CancellationToken.None);

        _hubClients.Verify(c => c.Client(ConnectionId), Times.Once);
        _clientProxy.Verify(p => p.SendCoreAsync(
            "ReceiveChannelValues",
            It.Is<object?[]>(args => args.Length == 1
                && ((ChannelValue[])args[0]!).Length == 1
                && ((ChannelValue[])args[0]!)[0].SessionIndex == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Execute_CarToCloudValue_NotForwarded()
    {
        var def = new ChannelDefinition { Distribution = ChannelDistribution.CarToCloud };
        SetupSessionIndexMap(CarKey, new Dictionary<ushort, ChannelDefinition> { [0] = def });
        SetupConnectionId(CarKey, ConnectionId);

        ChannelValue[] values = [new() { SessionIndex = 0, Value = "100", Timestamp = DateTime.UtcNow }];
        var done = SetupStreamReads([BuildEntry(CarKey, values)]);

        await _service.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _service.StopAsync(CancellationToken.None);

        _clientProxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Execute_CarLocalValue_NotForwarded()
    {
        var def = new ChannelDefinition { Distribution = ChannelDistribution.CarLocal };
        SetupSessionIndexMap(CarKey, new Dictionary<ushort, ChannelDefinition> { [2] = def });
        SetupConnectionId(CarKey, ConnectionId);

        ChannelValue[] values = [new() { SessionIndex = 2, Value = "x", Timestamp = DateTime.UtcNow }];
        var done = SetupStreamReads([BuildEntry(CarKey, values)]);

        await _service.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _service.StopAsync(CancellationToken.None);

        _clientProxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Execute_CloudLocalValue_NotForwarded()
    {
        var def = new ChannelDefinition { Distribution = ChannelDistribution.CloudLocal };
        SetupSessionIndexMap(CarKey, new Dictionary<ushort, ChannelDefinition> { [3] = def });
        SetupConnectionId(CarKey, ConnectionId);

        ChannelValue[] values = [new() { SessionIndex = 3, Value = "y", Timestamp = DateTime.UtcNow }];
        var done = SetupStreamReads([BuildEntry(CarKey, values)]);

        await _service.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _service.StopAsync(CancellationToken.None);

        _clientProxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Execute_MixedDistribution_OnlyCloudToCarForwarded()
    {
        SetupSessionIndexMap(CarKey, new Dictionary<ushort, ChannelDefinition>
        {
            [0] = new() { Distribution = ChannelDistribution.CarToCloud },
            [1] = new() { Distribution = ChannelDistribution.CloudToCar },
            [2] = new() { Distribution = ChannelDistribution.CarLocal },
            [3] = new() { Distribution = ChannelDistribution.CloudToCar },
        });
        SetupConnectionId(CarKey, ConnectionId);

        ChannelValue[] values =
        [
            new() { SessionIndex = 0, Value = "a", Timestamp = DateTime.UtcNow },
            new() { SessionIndex = 1, Value = "b", Timestamp = DateTime.UtcNow },
            new() { SessionIndex = 2, Value = "c", Timestamp = DateTime.UtcNow },
            new() { SessionIndex = 3, Value = "d", Timestamp = DateTime.UtcNow },
        ];
        var done = SetupStreamReads([BuildEntry(CarKey, values)]);

        await _service.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _service.StopAsync(CancellationToken.None);

        _clientProxy.Verify(p => p.SendCoreAsync(
            "ReceiveChannelValues",
            It.Is<object?[]>(args => args.Length == 1
                && ((ChannelValue[])args[0]!).Length == 2
                && ((ChannelValue[])args[0]!)[0].SessionIndex == 1
                && ((ChannelValue[])args[0]!)[1].SessionIndex == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Execute_UnknownSessionIndex_Skipped()
    {
        SetupSessionIndexMap(CarKey, new Dictionary<ushort, ChannelDefinition>
        {
            [0] = new() { Distribution = ChannelDistribution.CloudToCar },
        });
        SetupConnectionId(CarKey, ConnectionId);

        ChannelValue[] values =
        [
            new() { SessionIndex = 0, Value = "known", Timestamp = DateTime.UtcNow },
            new() { SessionIndex = 99, Value = "unknown", Timestamp = DateTime.UtcNow },
        ];
        var done = SetupStreamReads([BuildEntry(CarKey, values)]);

        await _service.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _service.StopAsync(CancellationToken.None);

        _clientProxy.Verify(p => p.SendCoreAsync(
            "ReceiveChannelValues",
            It.Is<object?[]>(args => args.Length == 1
                && ((ChannelValue[])args[0]!).Length == 1
                && ((ChannelValue[])args[0]!)[0].SessionIndex == 0),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Execute_NoActiveConfiguration_SkipsForwarding()
    {
        SetupSessionIndexMap(CarKey, null);

        ChannelValue[] values = [new() { SessionIndex = 0, Value = "v", Timestamp = DateTime.UtcNow }];
        var done = SetupStreamReads([BuildEntry(CarKey, values)]);

        await _service.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _service.StopAsync(CancellationToken.None);

        _clientProxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Execute_NoConnectionForCar_SkipsForwarding()
    {
        SetupSessionIndexMap(CarKey, new Dictionary<ushort, ChannelDefinition>
        {
            [0] = new() { Distribution = ChannelDistribution.CloudToCar },
        });
        SetupConnectionId(CarKey, connectionId: null);

        ChannelValue[] values = [new() { SessionIndex = 0, Value = "v", Timestamp = DateTime.UtcNow }];
        var done = SetupStreamReads([BuildEntry(CarKey, values)]);

        await _service.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _service.StopAsync(CancellationToken.None);

        _clientProxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Execute_StreamEntry_IsAckedAfterProcessing()
    {
        SetupSessionIndexMap(CarKey, new Dictionary<ushort, ChannelDefinition>
        {
            [0] = new() { Distribution = ChannelDistribution.CloudToCar },
        });
        SetupConnectionId(CarKey, ConnectionId);

        ChannelValue[] values = [new() { SessionIndex = 0, Value = "v", Timestamp = DateTime.UtcNow }];
        var done = SetupStreamReads([BuildEntry(CarKey, values, "7-0")]);

        await _service.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _service.StopAsync(CancellationToken.None);

        _db.Verify(d => d.StreamAcknowledgeAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(),
            It.Is<RedisValue>(id => id.ToString() == "7-0"),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [TestMethod]
    public async Task Execute_InvalidMessagePackPayload_StillAcksEntry()
    {
        byte[] garbage = [0xFF, 0xFE, 0x00, 0x01];
        var entry = new StreamEntry("2-0", [new NameValueEntry(CarKey, garbage)]);
        var done = SetupStreamReads([entry]);

        await _service.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _service.StopAsync(CancellationToken.None);

        _db.Verify(d => d.StreamAcknowledgeAsync(
            It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()), Times.Once);
        _clientProxy.Verify(p => p.SendCoreAsync(
            It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Execute_ConsumerGroupAlreadyExists_StartupContinuesNormally()
    {
        _db.Setup(d => d.StreamCreateConsumerGroupAsync(
               It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<RedisValue?>(),
               It.IsAny<bool>(), It.IsAny<CommandFlags>()))
           .ThrowsAsync(new RedisServerException("BUSYGROUP Consumer Group name already exists"));
        var done = SetupStreamReads([]);

        await _service.StartAsync(CancellationToken.None);
        await done.WaitAsync(TimeSpan.FromSeconds(2));
        await _service.StopAsync(CancellationToken.None);
    }
}
