using Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Racecar.Clients;
using Racecar.Pipeline;
using Racecar.Pipeline.Consumers;

namespace Racecar.Tests.Pipeline;

[TestClass]
public sealed class SignalRTransmitConsumerTests
{
    private sealed class FakeCloudClient : ICloudClient
    {
        public event Action<HubConnectionState>? ConnectionStatusChanged;
        public List<ChannelValue[]> Sends { get; } = [];
        public Task SendChannelValuesAsync(ChannelValue[] channelValues)
        {
            Sends.Add(channelValues);
            return Task.CompletedTask;
        }
        public void RaiseConnected() => ConnectionStatusChanged?.Invoke(HubConnectionState.Connected);
    }

    private static ActiveConfiguration BuildConfig(int channelId)
    {
        return ActiveConfiguration.Empty with
        {
            Channels = new Dictionary<int, ChannelDefinition>
            {
                [channelId] = new ChannelDefinition { },
            },
        };
    }

    [TestMethod]
    public async Task Delta_loop_sends_changes_at_100ms_cadence()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero));
        var client = new FakeCloudClient();
        var config = BuildConfig(1);

        var consumer = new CloudTransmitConsumer(client, () => config, time, NullLogger.Instance);
        consumer.Start();
        await Task.Delay(50, TestContext.CancellationToken); // let both loops register their initial Task.Delay timers

        await consumer.HandleAsync(new[] { new InternalChannelValue(1, 5.0, 0, time.GetUtcNow().UtcDateTime) }, default);

        time.Advance(TimeSpan.FromMilliseconds(100));
        await Task.Delay(200, TestContext.CancellationToken); // let the loop body run

        Assert.IsGreaterThanOrEqualTo(1, client.Sends.Count, "Delta should have been sent.");
        Assert.AreEqual(1, client.Sends[0].Length);
        Assert.AreEqual("5", client.Sends[0][0].Value);

        await consumer.DisposeAsync();
    }

    [TestMethod]
    public async Task Full_loop_sends_snapshot_at_2_5s_cadence()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero));
        var client = new FakeCloudClient();
        var config = BuildConfig(1);

        var consumer = new CloudTransmitConsumer(client, () => config, time, NullLogger.Instance);
        consumer.Start();
        await Task.Delay(50, TestContext.CancellationToken);
        await consumer.HandleAsync(new[] { new InternalChannelValue(1, 7.0, 0, time.GetUtcNow().UtcDateTime) }, default);

        time.Advance(TimeSpan.FromMilliseconds(2500));
        await Task.Delay(200, TestContext.CancellationToken);

        Assert.IsGreaterThanOrEqualTo(1, client.Sends.Count, "Full should have been sent.");
        Assert.AreEqual(1, client.Sends[0].Length);

        await consumer.DisposeAsync();
    }

    public TestContext TestContext { get; set; }
}
