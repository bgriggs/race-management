using global::ChannelProcessor.RedMist;
using Channels;
using Cloud.Shared.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedMist.TimingCommon.Models;

namespace Cloud.Tests.ChannelProcessor.RedMist;

/// <summary>
/// Unit tests for the patch → channel-publish translator. Covers:
///   - team-car filtering against the <c>CarConfiguration.Car</c> set,
///   - sparse-patch handling (nullable <c>OverallPosition</c>, <c>ClassPosition</c>, <c>IsInPit</c>),
///   - <c>SessionStatePatch.CurrentFlag</c> → per-team <c>RaceFlagState</c> emission,
///   - snapshot replay producing both per-team RaceFlagState and per-car publishes.
///
/// Reserved-channel GUIDs are hard-coded here because they live in
/// <c>Common.ReservedChannels.cs</c>; if those Guids ever drift, this test fails with a
/// useful "no publish with channel id X" message rather than silently passing the wrong shape.
/// </summary>
[TestClass]
public class RedMistChannelPublisherTests
{
    private const int TeamId = 7;

    private static readonly Guid PositionId      = Guid.Parse("4e70c2d0-d89c-4896-af7c-a286ceda9565");
    private static readonly Guid ClassPositionId = Guid.Parse("7e8153fd-7280-4bcf-a11b-2227b70daddb");
    private static readonly Guid InPitId         = Guid.Parse("da12563a-1167-4899-9956-700b0b693005");
    private static readonly Guid RaceFlagStateId = Guid.Parse("d5b2e9f4-3c1a-4e7d-9b8f-2e4f6c1d3b02");

    private Mock<ICarChannelPublisher> _car = null!;
    private Mock<ITeamChannelPublisher> _team = null!;
    private List<(int teamId, string car, IReadOnlyList<PublishedChannelValue> values)> _carCalls = null!;
    private List<(int teamId, TeamChannelValue[] values)> _teamCalls = null!;
    private FixedTimeProvider _time = null!;
    private RedMistChannelPublisher _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _carCalls = [];
        _teamCalls = [];

        _car = new Mock<ICarChannelPublisher>();
        _car.Setup(p => p.PublishAsync(It.IsAny<int>(), It.IsAny<string>(),
                    It.IsAny<IReadOnlyList<PublishedChannelValue>>(), It.IsAny<CancellationToken>()))
            .Callback((int teamId, string car, IReadOnlyList<PublishedChannelValue> values, CancellationToken _) =>
                _carCalls.Add((teamId, car, values.ToList())))
            .ReturnsAsync(0);

        _team = new Mock<ITeamChannelPublisher>();
        _team.Setup(p => p.PublishAsync(It.IsAny<int>(), It.IsAny<TeamChannelValue[]>(), It.IsAny<CancellationToken>()))
            .Callback((int teamId, TeamChannelValue[] values, CancellationToken _) =>
                _teamCalls.Add((teamId, values)))
            .Returns(Task.CompletedTask);

        _time = new FixedTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        _sut = new RedMistChannelPublisher(_car.Object, _team.Object, _time, NullLogger<RedMistChannelPublisher>.Instance);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // ---- PublishCarPatchesAsync ----

    [TestMethod]
    public async Task CarPatches_TeamCar_PublishesPositionClassPositionAndInPit()
    {
        var patches = new[]
        {
            new CarPositionPatch { Number = "42", OverallPosition = 3, ClassPosition = 1, IsInPit = false },
        };

        var published = await _sut.PublishCarPatchesAsync(TeamId, Set("42"), patches, default);

        Assert.AreEqual(1, published);
        Assert.HasCount(1, _carCalls);
        var (teamId, car, values) = _carCalls[0];
        Assert.AreEqual(TeamId, teamId);
        Assert.AreEqual("42", car);
        AssertValue(values, PositionId, "3");
        AssertValue(values, ClassPositionId, "1");
        AssertValue(values, InPitId, "0");
    }

    [TestMethod]
    public async Task CarPatches_NonTeamCar_DroppedSilently()
    {
        var patches = new[]
        {
            new CarPositionPatch { Number = "9", OverallPosition = 5, ClassPosition = 2, IsInPit = true },
        };

        var published = await _sut.PublishCarPatchesAsync(TeamId, Set("42"), patches, default);

        Assert.AreEqual(0, published);
        Assert.IsEmpty(_carCalls);
    }

    [TestMethod]
    public async Task CarPatches_MissingNumber_DroppedSilently()
    {
        var patches = new[]
        {
            new CarPositionPatch { Number = "", OverallPosition = 3, ClassPosition = 1, IsInPit = false },
        };

        var published = await _sut.PublishCarPatchesAsync(TeamId, Set("42"), patches, default);

        Assert.AreEqual(0, published);
        Assert.IsEmpty(_carCalls);
    }

    [TestMethod]
    public async Task CarPatches_SparsePatch_OnlyEmitsFieldsThatAreSet()
    {
        // OverallPosition is set; ClassPosition and IsInPit are null (sparse patch).
        var patches = new[]
        {
            new CarPositionPatch { Number = "42", OverallPosition = 4, ClassPosition = null, IsInPit = null },
        };

        await _sut.PublishCarPatchesAsync(TeamId, Set("42"), patches, default);

        Assert.HasCount(1, _carCalls);
        var (_, _, values) = _carCalls[0];
        Assert.HasCount(1, values);
        AssertValue(values, PositionId, "4");
    }

    [TestMethod]
    public async Task CarPatches_AllFieldsNull_NoPublishMade()
    {
        var patches = new[]
        {
            new CarPositionPatch { Number = "42", OverallPosition = null, ClassPosition = null, IsInPit = null },
        };

        var published = await _sut.PublishCarPatchesAsync(TeamId, Set("42"), patches, default);

        Assert.AreEqual(0, published);
        Assert.IsEmpty(_carCalls);
    }

    [TestMethod]
    public async Task CarPatches_InPitTrue_PublishedAsOne()
    {
        var patches = new[]
        {
            new CarPositionPatch { Number = "42", IsInPit = true },
        };

        await _sut.PublishCarPatchesAsync(TeamId, Set("42"), patches, default);

        AssertValue(_carCalls[0].values, InPitId, "1");
    }

    [TestMethod]
    public async Task CarPatches_MultiplePatches_OneCallPerTeamCar()
    {
        var patches = new[]
        {
            new CarPositionPatch { Number = "42", OverallPosition = 3 },
            new CarPositionPatch { Number = "9",  OverallPosition = 1 }, // not team — drop
            new CarPositionPatch { Number = "5",  OverallPosition = 7 },
        };

        var published = await _sut.PublishCarPatchesAsync(TeamId, Set("42", "5"), patches, default);

        Assert.AreEqual(2, published);
        Assert.HasCount(2, _carCalls);
        CollectionAssert.AreEquivalent(new[] { "42", "5" }, _carCalls.Select(c => c.car).ToArray());
    }

    [TestMethod]
    public async Task CarPatches_EmptyList_NoPublish()
    {
        var published = await _sut.PublishCarPatchesAsync(TeamId, Set("42"), Array.Empty<CarPositionPatch>(), default);

        Assert.AreEqual(0, published);
        Assert.IsEmpty(_carCalls);
    }

    // ---- PublishSessionPatchAsync ----

    [TestMethod]
    public async Task SessionPatch_KnownFlag_PublishesRaceFlagState()
    {
        var ok = await _sut.PublishSessionPatchAsync(TeamId, new SessionStatePatch { CurrentFlag = Flags.Yellow }, default);

        Assert.IsTrue(ok);
        Assert.HasCount(1, _teamCalls);
        var (teamId, values) = _teamCalls[0];
        Assert.AreEqual(TeamId, teamId);
        Assert.HasCount(1, values);
        Assert.AreEqual(RaceFlagStateId, values[0].ChannelId);
        Assert.AreEqual("Yellow", values[0].Value);
        Assert.AreEqual(_time.GetUtcNow().UtcDateTime, values[0].Timestamp);
    }

    [TestMethod]
    public async Task SessionPatch_NullFlag_NoPublish()
    {
        var ok = await _sut.PublishSessionPatchAsync(TeamId, new SessionStatePatch { CurrentFlag = null }, default);

        Assert.IsFalse(ok);
        Assert.IsEmpty(_teamCalls);
    }

    [TestMethod]
    public async Task SessionPatch_DoesNotPublishPerCar()
    {
        // SessionStatePatch.CarPositions can be non-null (carries CarPositionPatch entries),
        // but PublishSessionPatchAsync only handles flag — per-car is the caller's job
        // (it routes patches.CarPositions through PublishCarPatchesAsync separately).
        await _sut.PublishSessionPatchAsync(TeamId, new SessionStatePatch { CurrentFlag = Flags.Red }, default);

        Assert.IsEmpty(_carCalls);
    }

    // ---- PublishSnapshotAsync ----

    [TestMethod]
    public async Task Snapshot_PublishesFlagAndPerCarValues_ForTeamCarsOnly()
    {
        var snapshot = new SessionState
        {
            EventId = 1,
            SessionId = 9,
            SessionName = "Race",
            CurrentFlag = Flags.Green,
            CarPositions =
            [
                new CarPosition { Number = "42", OverallPosition = 3, ClassPosition = 1, IsInPit = false },
                new CarPosition { Number = "5",  OverallPosition = 4, ClassPosition = 2, IsInPit = true },
                new CarPosition { Number = "9",  OverallPosition = 5, ClassPosition = 3, IsInPit = false }, // not team
            ],
        };

        await _sut.PublishSnapshotAsync(TeamId, Set("42", "5"), snapshot, default);

        Assert.HasCount(1, _teamCalls);
        Assert.AreEqual("Green", _teamCalls[0].values[0].Value);

        Assert.HasCount(2, _carCalls);
        CollectionAssert.AreEquivalent(new[] { "42", "5" }, _carCalls.Select(c => c.car).ToArray());
        AssertValue(_carCalls.Single(c => c.car == "42").values, InPitId, "0");
        AssertValue(_carCalls.Single(c => c.car == "5").values, InPitId, "1");
    }

    [TestMethod]
    public async Task Snapshot_EmptyCarPositions_StillPublishesFlag()
    {
        var snapshot = new SessionState
        {
            EventId = 1,
            SessionId = 9,
            SessionName = "Race",
            CurrentFlag = Flags.Red,
            CarPositions = [],
        };

        await _sut.PublishSnapshotAsync(TeamId, Set("42"), snapshot, default);

        Assert.HasCount(1, _teamCalls);
        Assert.AreEqual("Red", _teamCalls[0].values[0].Value);
        Assert.IsEmpty(_carCalls);
    }

    [TestMethod]
    public async Task Snapshot_FlagWithoutMapping_SkippedButSnapshotStillProcessesCars()
    {
        // (Flags)int.MaxValue → null from the mapper → no team publish, but team cars still
        // get their per-car publishes from CarPositions.
        var snapshot = new SessionState
        {
            EventId = 1,
            SessionId = 9,
            SessionName = "Race",
            CurrentFlag = (Flags)int.MaxValue,
            CarPositions = [new CarPosition { Number = "42", OverallPosition = 3 }],
        };

        await _sut.PublishSnapshotAsync(TeamId, Set("42"), snapshot, default);

        Assert.IsEmpty(_teamCalls);
        Assert.HasCount(1, _carCalls);
    }

    // ---- helpers ----

    private static HashSet<string> Set(params string[] cars) => new(cars, StringComparer.OrdinalIgnoreCase);

    private static void AssertValue(IReadOnlyList<PublishedChannelValue> values, Guid channelId, string expectedValue)
    {
        var match = values.SingleOrDefault(v => v.ChannelId == channelId);
        Assert.AreNotEqual(default, match, $"No publish with channel id {channelId}; actual: [{string.Join(", ", values.Select(v => v.ChannelId))}]");
        Assert.AreEqual(expectedValue, match.Value);
    }
}
