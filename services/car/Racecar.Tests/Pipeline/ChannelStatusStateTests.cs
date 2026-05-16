using Channels;
using Racecar.Pipeline;

namespace Racecar.Tests.Pipeline;

[TestClass]
public sealed class ChannelStatusStateTests
{
    [TestMethod]
    public void RetainOnly_removes_channels_outside_set()
    {
        var s = new ChannelStatusState();
        s.Set(new InternalChannelValue(1, 1.0, 0, default));
        s.Set(new InternalChannelValue(2, 2.0, 0, default));
        s.Set(new InternalChannelValue(3, 3.0, 0, default));

        s.RetainOnly(new HashSet<int> { 1, 3 });

        Assert.IsTrue(s.TryGet(1, out _));
        Assert.IsFalse(s.TryGet(2, out _));
        Assert.IsTrue(s.TryGet(3, out _));
    }

    [TestMethod]
    public void Snapshot_reflects_current_values()
    {
        var s = new ChannelStatusState();
        s.Set(new InternalChannelValue(10, 42.0, 1, new DateTime(2026, 1, 1)));
        var snap = s.Snapshot();
        Assert.HasCount(1, snap);
        Assert.AreEqual(42.0, snap[10].Value);
    }
}
