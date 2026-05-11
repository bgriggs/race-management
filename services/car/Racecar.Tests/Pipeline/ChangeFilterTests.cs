using Channels;
using Racecar.Pipeline;

namespace Racecar.Tests.Pipeline;

[TestClass]
public sealed class ChangeFilterTests
{
    private static ActiveConfiguration ConfigWithDeadbands(params (int id, double deadband)[] beads)
    {
        return ActiveConfiguration.Empty with
        {
            Channels = beads.ToDictionary(b => b.id, _ => new ChannelDefinition()),
            Deadbands = beads.ToDictionary(b => b.id, b => b.deadband),
        };
    }

    [TestMethod]
    public void First_value_for_a_channel_passes()
    {
        var status = new ChannelStatusState();
        var filter = new ChangeFilter(status);
        var config = ConfigWithDeadbands((1, 0));

        var buffer = new[] { new InternalChannelValue(1, 5.0, 100, DateTime.UnixEpoch) };
        var kept = filter.Filter(config, buffer);

        Assert.AreEqual(1, kept);
        Assert.IsTrue(status.TryGet(1, out var v));
        Assert.AreEqual(5.0, v.BaseValue);
    }

    [TestMethod]
    public void Identical_value_is_dropped_when_deadband_is_zero()
    {
        var status = new ChannelStatusState();
        var filter = new ChangeFilter(status);
        var config = ConfigWithDeadbands((1, 0));

        _ = filter.Filter(config, new[] { new InternalChannelValue(1, 5.0, 100, DateTime.UnixEpoch) });
        var kept = filter.Filter(config, new[] { new InternalChannelValue(1, 5.0, 200, DateTime.UnixEpoch) });

        Assert.AreEqual(0, kept);
    }

    [TestMethod]
    public void Value_within_deadband_is_dropped()
    {
        var status = new ChannelStatusState();
        var filter = new ChangeFilter(status);
        var config = ConfigWithDeadbands((1, 0.5));

        _ = filter.Filter(config, new[] { new InternalChannelValue(1, 10.0, 0, default) });
        var kept = filter.Filter(config, new[] { new InternalChannelValue(1, 10.4, 0, default) });

        Assert.AreEqual(0, kept);
    }

    [TestMethod]
    public void Value_outside_deadband_passes()
    {
        var status = new ChannelStatusState();
        var filter = new ChangeFilter(status);
        var config = ConfigWithDeadbands((1, 0.5));

        _ = filter.Filter(config, new[] { new InternalChannelValue(1, 10.0, 0, default) });
        var kept = filter.Filter(config, new[] { new InternalChannelValue(1, 10.6, 0, default) });

        Assert.AreEqual(1, kept);
    }

    [TestMethod]
    public void Filter_packs_survivors_at_start_of_buffer()
    {
        var status = new ChannelStatusState();
        var filter = new ChangeFilter(status);
        var config = ConfigWithDeadbands((1, 0), (2, 0), (3, 0));

        // seed
        _ = filter.Filter(config, new[]
        {
            new InternalChannelValue(1, 1.0, 0, default),
            new InternalChannelValue(2, 2.0, 0, default),
            new InternalChannelValue(3, 3.0, 0, default),
        });

        var buf = new[]
        {
            new InternalChannelValue(1, 1.0, 1, default), // unchanged - drop
            new InternalChannelValue(2, 9.0, 1, default), // changed - keep
            new InternalChannelValue(3, 3.0, 1, default), // unchanged - drop
        };

        var kept = filter.Filter(config, buf);

        Assert.AreEqual(1, kept);
        Assert.AreEqual(2, buf[0].ChannelId);
        Assert.AreEqual(9.0, buf[0].BaseValue);
    }
}
