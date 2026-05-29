using Cloud.Shared.Database;
using Cloud.Shared.Database.Models;
using Cloud.Shared.Database.Models.FuelAnalysis;
using Microsoft.EntityFrameworkCore;
using WebApi.FuelAnalysis;

namespace Cloud.Tests.WebApi.FuelAnalysis;

/// <summary>
/// Engineer-initiated Stint CRUD + FuelWindow cascade rules. Telemetry-driven Stint
/// lifecycle (StintLifecycle, SessionLifecycleHandler) is covered in
/// <c>ChannelProcessor/FuelAnalysis</c> tests and is intentionally out of scope here.
/// </summary>
[TestClass]
public class StintEditorTests
{
    private const int TeamId = 1;
    private const string CarNumber = "42";
    private const int RaceId = 7;
    private static readonly DateTime T0 = new(2026, 5, 27, 10, 0, 0, DateTimeKind.Utc);

    private RaceManagementContext _db = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<RaceManagementContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new RaceManagementContext(options);

        _db.Teams.Add(new Team { Id = TeamId, Name = "T", ClientId = "client" });
        _db.Cars.Add(new Car { TeamId = TeamId, Number = CarNumber, Make = "M", Model = "Mo", Color = "C" });
        _db.Races.Add(new Race
        {
            Id = RaceId,
            TeamId = TeamId,
            Name = "R",
            Start = T0,
            Duration = 6,
            TimeZone = "UTC",
            Notes = "",
        });
        _db.SaveChanges();
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ---------- Create ----------

    [TestMethod]
    public async Task Create_HappyPath_WritesStintWindowRefuelAndAutoFollowOn()
    {
        var outcome = await StintEditor.CreateAsync(_db, TeamId, CarNumber, RaceId,
            startAt: T0, endAt: T0.AddMinutes(60), originType: StintOriginType.Manual, default);

        Assert.IsNull(outcome.Error);
        Assert.IsNotNull(outcome.Success);
        Assert.AreEqual(StintOriginType.Manual, outcome.Success!.Stint.OriginType);
        // 2 Stints: the Manual one + an Auto follow-on placeholder at its EndAt. The Auto
        // follow-on assumes a full refuel happened during the pit between them — it owns
        // a NEW FuelWindow + NEW Pending RefuelEvent, and projects EndAt out by
        // (TankCapacityGallons / DefaultConsumptionGalPerMin). When CarFuelConfig is
        // unconfigured (this test's case), EndAt stays null.
        var stints = await _db.Stints.OrderBy(s => s.StartAt).ToListAsync();
        Assert.HasCount(2, stints);
        Assert.AreEqual(StintOriginType.Manual, stints[0].OriginType);
        Assert.AreEqual(T0.AddMinutes(60), stints[0].EndAt);
        Assert.AreEqual(StintOriginType.Auto, stints[1].OriginType);
        Assert.AreEqual(T0.AddMinutes(60), stints[1].StartAt);
        Assert.IsNull(stints[1].EndAt);
        Assert.AreNotEqual(stints[0].FuelWindowId, stints[1].FuelWindowId);

        var windows = await _db.FuelWindows.OrderBy(w => w.OpenedAt).ToListAsync();
        Assert.HasCount(2, windows);
        Assert.AreEqual(T0.AddMinutes(60), windows[0].ClosedAt, "manual stint's window closes at the pit");
        Assert.AreEqual(T0.AddMinutes(60), windows[1].OpenedAt, "Auto follow-on's window opens at the same instant");
        Assert.IsNull(windows[1].ClosedAt);

        var refuels = await _db.RefuelEvents.OrderBy(r => r.DetectedAt).ToListAsync();
        Assert.HasCount(2, refuels);
        Assert.AreEqual("Pending", refuels[0].Status);
        Assert.AreEqual(outcome.Success.Window.StartRefuelEventId, refuels[0].Id);
        Assert.AreEqual("Pending", refuels[1].Status, "post-stint refuel is Pending — engineer confirms volume after stint ends");
        Assert.AreEqual(T0.AddMinutes(60), refuels[1].DetectedAt);
    }

    [TestMethod]
    public async Task Create_WithOpenPriorWindow_ClosesPriorAtStartAt()
    {
        // Seed: a prior open window + open stint, opened 30 min before T0.
        await SeedRefuelWindowAndStintAsync(openedAt: T0.AddMinutes(-30), stintEnd: null);

        var outcome = await StintEditor.CreateAsync(_db, TeamId, CarNumber, RaceId,
            startAt: T0, endAt: T0.AddMinutes(60), originType: StintOriginType.Manual, default);

        Assert.IsNull(outcome.Error);
        var allWindows = await _db.FuelWindows.OrderBy(w => w.OpenedAt).ToListAsync();
        Assert.HasCount(2, allWindows);
        Assert.AreEqual(T0, allWindows[0].ClosedAt);
        Assert.AreEqual(outcome.Success!.Refuel.Id, allWindows[0].EndRefuelEventId);
        var openStintInPrior = await _db.Stints.FirstAsync(s => s.FuelWindowId == allWindows[0].Id);
        Assert.AreEqual(T0, openStintInPrior.EndAt);
    }

    [TestMethod]
    public async Task Create_EndBeforeStart_Rejected()
    {
        var outcome = await StintEditor.CreateAsync(_db, TeamId, CarNumber, RaceId,
            startAt: T0, endAt: T0.AddMinutes(-1), originType: StintOriginType.Manual, default);

        Assert.AreEqual(StintEditor.CreateError.EndBeforeStart, outcome.Error);
        Assert.AreEqual(0, await _db.Stints.CountAsync());
    }

    [TestMethod]
    public async Task Create_RaceNotFound_Rejected()
    {
        var outcome = await StintEditor.CreateAsync(_db, TeamId, CarNumber, raceId: 999,
            startAt: T0, endAt: T0.AddMinutes(60), originType: StintOriginType.Manual, default);

        Assert.AreEqual(StintEditor.CreateError.RaceNotFound, outcome.Error);
    }

    [TestMethod]
    public async Task Create_CarNotFound_Rejected()
    {
        var outcome = await StintEditor.CreateAsync(_db, TeamId, carNumber: "99", raceId: RaceId,
            startAt: T0, endAt: T0.AddMinutes(60), originType: StintOriginType.Manual, default);

        Assert.AreEqual(StintEditor.CreateError.CarNotFound, outcome.Error);
    }

    // ---------- Update ----------

    [TestMethod]
    public async Task Update_SimpleFieldChange_NoCascade_OpensAutoFollowOn()
    {
        var stintId = (await SeedRefuelWindowAndStintAsync(openedAt: T0, stintEnd: T0.AddMinutes(60))).stintId;

        var outcome = await StintEditor.UpdateAsync(_db, TeamId, stintId,
            startAt: T0, endAt: T0.AddMinutes(75), originType: StintOriginType.Manual,
            noFuelOnPit: false, default);

        Assert.IsNull(outcome.Error);
        // Seeded-stint is the only stint (first-stint rule) → an Auto follow-on opens at
        // its new EndAt, in a NEW FuelWindow + NEW Pending RefuelEvent (full-refuel default).
        var stints = await _db.Stints.OrderBy(s => s.StartAt).ToListAsync();
        Assert.HasCount(2, stints);
        Assert.AreEqual(T0.AddMinutes(75), stints[0].EndAt);
        Assert.AreEqual(StintOriginType.Manual, stints[0].OriginType);
        Assert.AreEqual(StintOriginType.Auto, stints[1].OriginType);
        Assert.AreEqual(T0.AddMinutes(75), stints[1].StartAt);
        Assert.IsNull(stints[1].EndAt);
        Assert.AreNotEqual(stints[0].FuelWindowId, stints[1].FuelWindowId);
        Assert.AreEqual(2, await _db.FuelWindows.CountAsync());
        Assert.AreEqual(2, await _db.RefuelEvents.CountAsync());
    }

    [TestMethod]
    public async Task Update_EndBeforeStart_Rejected()
    {
        var stintId = (await SeedRefuelWindowAndStintAsync(openedAt: T0, stintEnd: T0.AddMinutes(60))).stintId;

        var outcome = await StintEditor.UpdateAsync(_db, TeamId, stintId,
            startAt: T0, endAt: T0.AddMinutes(-1), originType: StintOriginType.Manual,
            noFuelOnPit: false, default);

        Assert.AreEqual(StintEditor.UpdateError.EndBeforeStart, outcome.Error);
    }

    [TestMethod]
    public async Task Update_NoFuelOnPit_FirstInWindow_MergesIntoPriorWindow()
    {
        // Seed: prior window (W1) with stint S1, then current window (W2) with stint S2.
        // Mark "no fuel on pit" on S2 → W2 dissolves, S2 joins W1.
        var (priorRefuelId, w1Id, s1Id) = await SeedRefuelWindowAndStintAsync(
            openedAt: T0, stintEnd: T0.AddMinutes(60));
        // Close W1 to mimic the natural sequence — at T0+60, a new refuel happened.
        var w1 = await _db.FuelWindows.FirstAsync(w => w.Id == w1Id);
        w1.ClosedAt = T0.AddMinutes(60);

        var refuel2 = new RefuelEvent
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            RaceId = RaceId,
            DetectedAt = T0.AddMinutes(60),
            ConfidenceTier = RefuelConfidenceTier.Manual,
            Source = RefuelSource.Manual,
            Status = "Pending",
        };
        _db.RefuelEvents.Add(refuel2);
        await _db.SaveChangesAsync();

        w1.EndRefuelEventId = refuel2.Id;

        var w2 = new FuelWindow
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            RaceId = RaceId,
            StartRefuelEventId = refuel2.Id,
            OpenedAt = T0.AddMinutes(60),
        };
        _db.FuelWindows.Add(w2);
        await _db.SaveChangesAsync();

        var s2 = new Stint
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            RaceId = RaceId,
            FuelWindowId = w2.Id,
            StartAt = T0.AddMinutes(60),
            EndAt = T0.AddMinutes(120),
            OriginType = StintOriginType.Auto,
        };
        _db.Stints.Add(s2);
        await _db.SaveChangesAsync();

        var outcome = await StintEditor.UpdateAsync(_db, TeamId, s2.Id,
            startAt: T0.AddMinutes(60), endAt: T0.AddMinutes(120), originType: StintOriginType.Auto,
            noFuelOnPit: true, default);

        Assert.IsNull(outcome.Error);
        Assert.AreEqual(1, await _db.FuelWindows.CountAsync(), "W2 should be deleted");
        Assert.AreEqual(1, await _db.RefuelEvents.CountAsync(), "refuel2 should be deleted");
        var s2After = await _db.Stints.FirstAsync(s => s.Id == s2.Id);
        Assert.AreEqual(w1Id, s2After.FuelWindowId);

        var w1After = await _db.FuelWindows.FirstAsync(w => w.Id == w1Id);
        Assert.IsNull(w1After.ClosedAt, "W1 should re-open since the refuel that closed it is gone");
        Assert.IsNull(w1After.EndRefuelEventId);
    }

    [TestMethod]
    public async Task Update_NoFuelOnPit_NoPriorWindow_Rejected()
    {
        var stintId = (await SeedRefuelWindowAndStintAsync(openedAt: T0, stintEnd: T0.AddMinutes(60))).stintId;

        var outcome = await StintEditor.UpdateAsync(_db, TeamId, stintId,
            startAt: T0, endAt: T0.AddMinutes(60), originType: StintOriginType.Auto,
            noFuelOnPit: true, default);

        Assert.AreEqual(StintEditor.UpdateError.NoPriorWindowToMerge, outcome.Error);
    }

    [TestMethod]
    public async Task Update_NoFuelOnPit_False_NotFirstInWindow_SplitsWindow()
    {
        // Seed: one window W1, two stints S1 and S2 chained (S2 is a non-fuel-pit follower).
        var (_, w1Id, s1Id) = await SeedRefuelWindowAndStintAsync(
            openedAt: T0, stintEnd: T0.AddMinutes(60));
        var s1 = await _db.Stints.FirstAsync(s => s.Id == s1Id);

        var s2 = new Stint
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            RaceId = RaceId,
            FuelWindowId = w1Id,
            StartAt = T0.AddMinutes(65),
            EndAt = T0.AddMinutes(125),
            OriginType = StintOriginType.Auto,
        };
        _db.Stints.Add(s2);
        await _db.SaveChangesAsync();

        var outcome = await StintEditor.UpdateAsync(_db, TeamId, s2.Id,
            startAt: T0.AddMinutes(65), endAt: T0.AddMinutes(125), originType: StintOriginType.Auto,
            noFuelOnPit: false, default);

        Assert.IsNull(outcome.Error);
        Assert.AreEqual(2, await _db.FuelWindows.CountAsync(), "should now be split into two windows");
        Assert.AreEqual(2, await _db.RefuelEvents.CountAsync(), "a new refuel was created for the split");

        var s1After = await _db.Stints.FirstAsync(s => s.Id == s1Id);
        var s2After = await _db.Stints.FirstAsync(s => s.Id == s2.Id);
        Assert.AreNotEqual(s1After.FuelWindowId, s2After.FuelWindowId);
    }

    [TestMethod]
    public async Task Update_NoFuelOnPit_StateMatchesCurrent_NoOp()
    {
        // Stint is first-in-its-window; setting noFuelOnPit=false (matches current) is a no-op.
        var stintId = (await SeedRefuelWindowAndStintAsync(openedAt: T0, stintEnd: T0.AddMinutes(60))).stintId;

        var outcome = await StintEditor.UpdateAsync(_db, TeamId, stintId,
            startAt: T0, endAt: T0.AddMinutes(60), originType: StintOriginType.Auto,
            noFuelOnPit: false, default);

        Assert.IsNull(outcome.Error);
        Assert.AreEqual(1, await _db.FuelWindows.CountAsync());
        Assert.AreEqual(1, await _db.RefuelEvents.CountAsync());
    }

    // ---------- Delete ----------

    [TestMethod]
    public async Task Delete_StintNotFound_Reports()
    {
        var outcome = await StintEditor.DeleteAsync(_db, TeamId, stintId: 12345, default);

        Assert.AreEqual(StintEditor.DeleteError.StintNotFound, outcome.Error);
    }

    [TestMethod]
    public async Task Delete_NonFirstInWindow_OnlyStintRemoved()
    {
        var (_, w1Id, s1Id) = await SeedRefuelWindowAndStintAsync(openedAt: T0, stintEnd: T0.AddMinutes(60));
        var s2 = new Stint
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            RaceId = RaceId,
            FuelWindowId = w1Id,
            StartAt = T0.AddMinutes(65),
            EndAt = T0.AddMinutes(125),
            OriginType = StintOriginType.Auto,
        };
        _db.Stints.Add(s2);
        await _db.SaveChangesAsync();

        var outcome = await StintEditor.DeleteAsync(_db, TeamId, s2.Id, default);

        Assert.IsTrue(outcome.Success);
        Assert.AreEqual(1, await _db.Stints.CountAsync());
        Assert.AreEqual(1, await _db.FuelWindows.CountAsync());
        Assert.AreEqual(1, await _db.RefuelEvents.CountAsync());
    }

    [TestMethod]
    public async Task Delete_OwnsWindowNoFollowers_DropsWindowAndRefuel()
    {
        var stintId = (await SeedRefuelWindowAndStintAsync(openedAt: T0, stintEnd: T0.AddMinutes(60))).stintId;

        var outcome = await StintEditor.DeleteAsync(_db, TeamId, stintId, default);

        Assert.IsTrue(outcome.Success);
        Assert.AreEqual(0, await _db.Stints.CountAsync());
        Assert.AreEqual(0, await _db.FuelWindows.CountAsync());
        Assert.AreEqual(0, await _db.RefuelEvents.CountAsync());
    }

    [TestMethod]
    public async Task Delete_OwnsWindowWithFollowers_NoPriorWindow_Rejected()
    {
        var (_, w1Id, s1Id) = await SeedRefuelWindowAndStintAsync(openedAt: T0, stintEnd: T0.AddMinutes(60));
        var s2 = new Stint
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            RaceId = RaceId,
            FuelWindowId = w1Id,
            StartAt = T0.AddMinutes(65),
            EndAt = T0.AddMinutes(125),
            OriginType = StintOriginType.Auto,
        };
        _db.Stints.Add(s2);
        await _db.SaveChangesAsync();

        var outcome = await StintEditor.DeleteAsync(_db, TeamId, s1Id, default);

        Assert.AreEqual(StintEditor.DeleteError.OwnsFirstWindowAndHasFollowers, outcome.Error);
    }

    [TestMethod]
    public async Task Delete_OwnsWindowWithFollowers_HasPriorWindow_MergesFollowersIntoPrior()
    {
        // Seed: W1 with stint S1 (T0..T0+60), W2 with stints S2 (T0+60..T0+120) and S3 (T0+125..T0+185).
        // Delete S2 → W2 dissolves, S3 joins W1.
        var (_, w1Id, _) = await SeedRefuelWindowAndStintAsync(openedAt: T0, stintEnd: T0.AddMinutes(60));
        var w1 = await _db.FuelWindows.FirstAsync(w => w.Id == w1Id);
        w1.ClosedAt = T0.AddMinutes(60);

        var refuel2 = new RefuelEvent
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            RaceId = RaceId,
            DetectedAt = T0.AddMinutes(60),
            ConfidenceTier = RefuelConfidenceTier.Manual,
            Source = RefuelSource.Manual,
            Status = "Pending",
        };
        _db.RefuelEvents.Add(refuel2);
        await _db.SaveChangesAsync();
        w1.EndRefuelEventId = refuel2.Id;

        var w2 = new FuelWindow
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            RaceId = RaceId,
            StartRefuelEventId = refuel2.Id,
            OpenedAt = T0.AddMinutes(60),
        };
        _db.FuelWindows.Add(w2);
        await _db.SaveChangesAsync();

        var s2 = new Stint
        {
            TeamId = TeamId, CarNumber = CarNumber, RaceId = RaceId,
            FuelWindowId = w2.Id,
            StartAt = T0.AddMinutes(60),
            EndAt = T0.AddMinutes(120),
            OriginType = StintOriginType.Auto,
        };
        var s3 = new Stint
        {
            TeamId = TeamId, CarNumber = CarNumber, RaceId = RaceId,
            FuelWindowId = w2.Id,
            StartAt = T0.AddMinutes(125),
            EndAt = T0.AddMinutes(185),
            OriginType = StintOriginType.Auto,
        };
        _db.Stints.AddRange(s2, s3);
        await _db.SaveChangesAsync();

        var outcome = await StintEditor.DeleteAsync(_db, TeamId, s2.Id, default);

        Assert.IsTrue(outcome.Success);
        Assert.AreEqual(1, await _db.FuelWindows.CountAsync(), "W2 should be removed");
        Assert.AreEqual(1, await _db.RefuelEvents.CountAsync(), "refuel2 should be removed");

        var s3After = await _db.Stints.FirstAsync(s => s.Id == s3.Id);
        Assert.AreEqual(w1Id, s3After.FuelWindowId, "S3 should be reparented to W1");
    }

    // ---------- helpers ----------

    private async Task<(int refuelId, int windowId, int stintId)> SeedRefuelWindowAndStintAsync(DateTime openedAt, DateTime? stintEnd)
    {
        var refuel = new RefuelEvent
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            RaceId = RaceId,
            DetectedAt = openedAt,
            ConfidenceTier = RefuelConfidenceTier.Manual,
            Source = RefuelSource.SessionStart,
            Status = "Pending",
        };
        _db.RefuelEvents.Add(refuel);
        await _db.SaveChangesAsync();

        var window = new FuelWindow
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            RaceId = RaceId,
            StartRefuelEventId = refuel.Id,
            OpenedAt = openedAt,
        };
        _db.FuelWindows.Add(window);
        await _db.SaveChangesAsync();

        var stint = new Stint
        {
            TeamId = TeamId,
            CarNumber = CarNumber,
            RaceId = RaceId,
            FuelWindowId = window.Id,
            StartAt = openedAt,
            EndAt = stintEnd,
            OriginType = StintOriginType.Auto,
        };
        _db.Stints.Add(stint);
        await _db.SaveChangesAsync();

        return (refuel.Id, window.Id, stint.Id);
    }
}
