using Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Racecar.Pipeline;
using Racecar.Pipeline.Consumers;

namespace Racecar.Tests.Pipeline;

[TestClass]
public sealed class SignalRTransmitConsumerTests
{
    private sealed class FakeTarget : ISignalRTarget
    {
        public string Name => "fake";
        public bool IsConnected { get; set; } = true;
        public List<List<ChannelValue>> Deltas { get; } = [];
        public List<List<ChannelValue>> Fulls { get; } = [];
        public event Action? Reconnected;
        public Task SendDeltaAsync(IReadOnlyList<ChannelValue> values, CancellationToken ct)
        {
            Deltas.Add([.. values]);
            return Task.CompletedTask;
        }
        public Task SendFullAsync(IReadOnlyList<ChannelValue> values, CancellationToken ct)
        {
            Fulls.Add([.. values]);
            return Task.CompletedTask;
        }
        public void RaiseReconnected() => Reconnected?.Invoke();
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
    public async Task Delta_loop_sends_changes_at_100ms_cadence_when_connected()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero));
        var target = new FakeTarget();
        var config = BuildConfig(1);

        var consumer = new SignalRTransmitConsumer(target, () => config, time, NullLogger.Instance);
        consumer.Start();
        await Task.Delay(50, TestContext.CancellationToken); // let both loops register their initial Task.Delay timers

        await consumer.HandleAsync(new[] { new InternalChannelValue(1, 5.0, 0, time.GetUtcNow().UtcDateTime) }, default);

        time.Advance(TimeSpan.FromMilliseconds(100));
        await Task.Delay(200, TestContext.CancellationToken); // let the loop body run

        Assert.IsGreaterThanOrEqualTo(1, target.Deltas.Count, "Delta should have been sent.");
        Assert.HasCount(1, target.Deltas[0]);
        Assert.AreEqual("5", target.Deltas[0][0].Value);

        await consumer.DisposeAsync();
    }

    [TestMethod]
    public async Task Full_loop_sends_snapshot_at_2_5s_cadence()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero));
        var target = new FakeTarget();
        var config = BuildConfig(1);

        var consumer = new SignalRTransmitConsumer(target, () => config, time, NullLogger.Instance);
        consumer.Start();
        await Task.Delay(50, TestContext.CancellationToken);
        await consumer.HandleAsync(new[] { new InternalChannelValue(1, 7.0, 0, time.GetUtcNow().UtcDateTime) }, default);

        time.Advance(TimeSpan.FromMilliseconds(2500));
        await Task.Delay(200, TestContext.CancellationToken);

        Assert.IsGreaterThanOrEqualTo(1, target.Fulls.Count, "Full should have been sent.");
        Assert.HasCount(1, target.Fulls[0]);

        await consumer.DisposeAsync();
    }

    [TestMethod]
    public async Task While_disconnected_no_send_occurs()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero));
        var target = new FakeTarget { IsConnected = false };
        var config = BuildConfig(1);

        var consumer = new SignalRTransmitConsumer(target, () => config, time, NullLogger.Instance);
        consumer.Start();
        await Task.Delay(50, TestContext.CancellationToken);
        await consumer.HandleAsync(new[] { new InternalChannelValue(1, 1.0, 0, default) }, default);

        time.Advance(TimeSpan.FromSeconds(3));
        await Task.Delay(200, TestContext.CancellationToken);

        Assert.IsEmpty(target.Deltas);
        Assert.IsEmpty(target.Fulls);

        await consumer.DisposeAsync();
    }

    public TestContext TestContext { get; set; }
}
