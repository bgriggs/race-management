using Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Racecar.Pipeline;
using Racecar.Pipeline.Dispatch;

namespace Racecar.Tests.Pipeline;

[TestClass]
public sealed class ChannelTimeoutMonitorTests
{
    private static ActiveConfiguration ConfigWith(int channelId, int timeoutMs, double defaultValue)
    {
        var def = new ChannelDefinition { TimeoutMs = timeoutMs, DefaultValue = defaultValue };
        return ActiveConfiguration.Empty with
        {
            Channels = new Dictionary<int, ChannelDefinition> { [channelId] = def },
            Deadbands = new Dictionary<int, double> { [channelId] = 0d },
        };
    }

    private static ChannelTimeoutMonitor Monitor(
        ActiveConfiguration config, ChannelStatusState state, FakeTimeProvider time) =>
        new(() => config, state, () => Array.Empty<ChannelConsumerHost>(), time, NullLogger.Instance);

    private static void Feed(ChangeFilter filter, ActiveConfiguration config, int channelId, double value, FakeTimeProvider time)
    {
        var now = time.GetUtcNow().UtcDateTime;
        _ = filter.Filter(config, [new InternalChannelValue(channelId, value, time.GetTimestamp(), now)]);
    }

    [TestMethod]
    public void Constant_value_with_active_bus_is_not_reset_to_default()
    {
        // Regression: the timeout must measure time since the last *sample*, not the last value
        // *change*. A live CAN source sending an unchanged value far faster than TimeoutMs must
        // never be reset to default, even though the change filter drops every repeat.
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 24, 0, 0, 0, TimeSpan.Zero));
        var state = new ChannelStatusState();
        var filter = new ChangeFilter(state);
        var config = ConfigWith(channelId: 1, timeoutMs: 3000, defaultValue: 0.0);
        var monitor = Monitor(config, state, time);

        // 10 s of activity at 10 Hz — well past the 3 s timeout — with a constant value.
        for (var i = 0; i < 100; i++)
        {
            Feed(filter, config, channelId: 1, value: 5.0, time);
            time.Advance(TimeSpan.FromMilliseconds(100));
            monitor.CheckTimeouts();

            Assert.IsTrue(state.TryGet(1, out var v));
            Assert.AreEqual(5.0, v.Value, $"value was reset to default on iteration {i}");
        }
    }

    [TestMethod]
    public void Value_is_reset_to_default_after_source_goes_silent()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 24, 0, 0, 0, TimeSpan.Zero));
        var state = new ChannelStatusState();
        var filter = new ChangeFilter(state);
        var config = ConfigWith(channelId: 1, timeoutMs: 3000, defaultValue: 0.0);
        var monitor = Monitor(config, state, time);

        Feed(filter, config, channelId: 1, value: 5.0, time);

        // Just under the timeout: still live.
        time.Advance(TimeSpan.FromMilliseconds(2900));
        monitor.CheckTimeouts();
        Assert.IsTrue(state.TryGet(1, out var live));
        Assert.AreEqual(5.0, live.Value);

        // Past the timeout with no further samples: reset to default.
        time.Advance(TimeSpan.FromMilliseconds(200));
        monitor.CheckTimeouts();
        Assert.IsTrue(state.TryGet(1, out var timedOut));
        Assert.AreEqual(0.0, timedOut.Value);
    }

    [TestMethod]
    public void Value_recovers_after_a_timeout_when_samples_resume()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 24, 0, 0, 0, TimeSpan.Zero));
        var state = new ChannelStatusState();
        var filter = new ChangeFilter(state);
        var config = ConfigWith(channelId: 1, timeoutMs: 3000, defaultValue: 0.0);
        var monitor = Monitor(config, state, time);

        Feed(filter, config, channelId: 1, value: 5.0, time);
        time.Advance(TimeSpan.FromMilliseconds(3100));
        monitor.CheckTimeouts();
        Assert.IsTrue(state.TryGet(1, out var timedOut));
        Assert.AreEqual(0.0, timedOut.Value);

        // Data resumes — the same constant value must publish again (it differs from the default).
        Feed(filter, config, channelId: 1, value: 5.0, time);
        monitor.CheckTimeouts();
        Assert.IsTrue(state.TryGet(1, out var recovered));
        Assert.AreEqual(5.0, recovered.Value);
    }
}
