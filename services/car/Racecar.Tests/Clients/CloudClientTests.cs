using Channels;
using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Racecar.Clients;

namespace Racecar.Tests.Clients;

/// <summary>
/// Focused tests for the CloudClient receive-path semantics. The actual hub.On
/// registration is exercised end-to-end by SignalR; what these tests pin down is
/// the OnChannelValuesReceived adapter behavior (event dispatch, exception
/// swallowing) so that subscribers cannot break the receive pipeline.
/// </summary>
[TestClass]
public sealed class CloudClientTests
{
    private sealed class StubOptionsMonitor : IOptionsMonitor<CarConfiguration>
    {
        public CarConfiguration CurrentValue { get; } = new() { Car = "TestCar", ClientId = "client", ClientSecret = "secret" };
        public CarConfiguration Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<CarConfiguration, string?> listener) => null;
    }

    private static CloudClient BuildClient()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new CloudClient(NullLoggerFactory.Instance, configuration, new StubOptionsMonitor());
    }

    [TestMethod]
    public void OnChannelValuesReceived_RaisesEvent_WithReceivedPayload()
    {
        var client = BuildClient();
        ChannelValue[]? captured = null;
        ((ICloudClient)client).ChannelValuesReceived += v => captured = v;

        ChannelValue[] payload =
        [
            new() { SessionIndex = 3, Value = "1.5", Timestamp = DateTime.UtcNow },
        ];
        client.OnChannelValuesReceived(payload);

        Assert.IsNotNull(captured);
        Assert.HasCount(1, captured);
        Assert.AreEqual<ushort>(3, captured[0].SessionIndex);
        Assert.AreEqual("1.5", captured[0].Value);
    }

    [TestMethod]
    public void OnChannelValuesReceived_NoSubscribers_DoesNotThrow()
    {
        var client = BuildClient();
        client.OnChannelValuesReceived([new ChannelValue { SessionIndex = 0, Value = "x", Timestamp = DateTime.UtcNow }]);
    }

    [TestMethod]
    public void OnChannelValuesReceived_SubscriberThrows_ExceptionIsSwallowed()
    {
        var client = BuildClient();
        ((ICloudClient)client).ChannelValuesReceived += _ => throw new InvalidOperationException("boom");

        // Should not propagate
        client.OnChannelValuesReceived([new ChannelValue { SessionIndex = 0, Value = "x", Timestamp = DateTime.UtcNow }]);
    }

    [TestMethod]
    public void OnChannelValuesReceived_MultipleSubscribers_AllInvoked()
    {
        var client = BuildClient();
        int hits = 0;
        ((ICloudClient)client).ChannelValuesReceived += _ => Interlocked.Increment(ref hits);
        ((ICloudClient)client).ChannelValuesReceived += _ => Interlocked.Increment(ref hits);

        client.OnChannelValuesReceived([new ChannelValue { SessionIndex = 0, Value = "x", Timestamp = DateTime.UtcNow }]);

        Assert.AreEqual(2, hits);
    }
}
