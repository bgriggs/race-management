using Racecar.Pipeline;
using Racecar.Pipeline.Dispatch;

namespace Racecar.Tests.Pipeline;

[TestClass]
public sealed class CoalescingMailboxTests
{
    [TestMethod]
    public async Task Coalesces_latest_per_channel()
    {
        var mb = new CoalescingMailbox(capacity: 16);
        mb.Write(new InternalChannelValue(1, 1.0, 0, default));
        mb.Write(new InternalChannelValue(2, 2.0, 0, default));
        mb.Write(new InternalChannelValue(1, 99.0, 0, default));

        var batch = await mb.DrainAsync(CancellationToken.None);

        Assert.HasCount(2, batch);
        var byId = batch.ToDictionary(v => v.ChannelId);
        Assert.AreEqual(99.0, byId[1].BaseValue);
        Assert.AreEqual(2.0, byId[2].BaseValue);
    }

    [TestMethod]
    public async Task DrainAsync_returns_batch_then_blocks_for_next()
    {
        var mb = new CoalescingMailbox(capacity: 8);
        mb.Write(new InternalChannelValue(1, 1.0, 0, default));

        var first = await mb.DrainAsync(CancellationToken.None);
        Assert.HasCount(1, first);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => _ = await mb.DrainAsync(cts.Token));
    }

    [TestMethod]
    public void Overflow_evicts_oldest_distinct_channel_and_counts_drops()
    {
        var mb = new CoalescingMailbox(capacity: 2);
        mb.Write(new InternalChannelValue(1, 1.0, 0, default));
        mb.Write(new InternalChannelValue(2, 2.0, 0, default));
        mb.Write(new InternalChannelValue(3, 3.0, 0, default));

        Assert.AreEqual(1, mb.Dropped);
        Assert.AreEqual(2, mb.PendingCount);
    }

    [TestMethod]
    public void Coalesce_overwrite_does_not_count_as_drop()
    {
        var mb = new CoalescingMailbox(capacity: 1);
        mb.Write(new InternalChannelValue(1, 1.0, 0, default));
        mb.Write(new InternalChannelValue(1, 2.0, 0, default));
        mb.Write(new InternalChannelValue(1, 3.0, 0, default));

        Assert.AreEqual(0, mb.Dropped);
    }
}

[TestClass]
public sealed class BoundedMailboxTests
{
    [TestMethod]
    public async Task DropOldest_writes_succeed_and_count_drops_when_full()
    {
        var mb = new BoundedMailbox<int>(capacity: 2, dropOldest: true);
        Assert.IsTrue(mb.Write(1));
        Assert.IsTrue(mb.Write(2));
        Assert.IsTrue(mb.Write(3)); // drops oldest (1)

        Assert.IsGreaterThanOrEqualTo(1, mb.Dropped);

        mb.Complete();
        var seen = new List<int>();
        await foreach (var v in mb.ReadAllAsync(CancellationToken.None)) seen.Add(v);
        CollectionAssert.AreEqual(new[] { 2, 3 }, seen);
    }

    [TestMethod]
    public void Lossless_returns_false_and_counts_drops_when_full()
    {
        var mb = new BoundedMailbox<int>(capacity: 2, dropOldest: false);
        Assert.IsTrue(mb.Write(1));
        Assert.IsTrue(mb.Write(2));
        Assert.IsFalse(mb.Write(3));

        Assert.AreEqual(1, mb.Dropped);
    }
}
