using global::ChannelProcessor.RedMist;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cloud.Tests.ChannelProcessor.RedMist;

/// <summary>
/// Unit tests for ADR-0008's activation rule. The evaluator's job is to pick the right
/// <c>Race</c> for a team given <c>nowUtc</c>, applying:
///   - 30-min pre / 30-min post pad around <c>(Race.Start, Race.Start+Race.Duration)</c>,
///   - tie-break by smallest |Start - now|, then by Race.Id ascending,
///   - skip races without either <c>RedMistEventId</c> or <c>RedMistOrganizationId</c>
///     (org-only rows are kept so the worker can resolve them to a live event id),
///   - resolve <c>Race.Start</c> through <c>Race.TimeZone</c> (IANA) into UTC.
///
/// Plus the supporting <c>LoadTeamCarNumbersAsync</c> lookup over <c>CarConfiguration.Car</c>.
/// </summary>
[TestClass]
public class RedMistActivationEvaluatorTests
{
    private const int TeamId = 7;
    private const string Tz = "America/Chicago"; // Race.Start default

    private RaceManagementContext _db = null!;
    private TestDbContextFactory _factory = null!;
    private RedMistActivationEvaluator _eval = null!;

    [TestInitialize]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<RaceManagementContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _db = new RaceManagementContext(options);
        _factory = new TestDbContextFactory(options);
        _eval = new RedMistActivationEvaluator(_factory, NullLogger<RedMistActivationEvaluator>.Instance);
    }

    [TestCleanup]
    public void Cleanup() => _db.Dispose();

    // ----- ListCandidateTeamsAsync -----

    [TestMethod]
    public async Task ListCandidateTeams_OnlyTeamsWithRedMistPairedRaces()
    {
        AddRace(teamId: 1, startWallClock: Wall(2026, 6, 1, 10, 00), redMistEventId: 100);
        AddRace(teamId: 2, startWallClock: Wall(2026, 6, 1, 10, 00), redMistEventId: null);
        AddRace(teamId: 3, startWallClock: Wall(2026, 6, 1, 10, 00), redMistEventId: 300);
        // duplicate team — should be deduped
        AddRace(teamId: 3, startWallClock: Wall(2026, 6, 2, 10, 00), redMistEventId: 301);
        await _db.SaveChangesAsync();

        var teams = await _eval.ListCandidateTeamsAsync(default);

        CollectionAssert.AreEquivalent(new[] { 1, 3 }, teams.ToArray());
    }

    [TestMethod]
    public async Task ListCandidateTeams_EmptyWhenNoneConfigured()
    {
        AddRace(teamId: 1, startWallClock: Wall(2026, 6, 1, 10, 00), redMistEventId: null);
        await _db.SaveChangesAsync();

        var teams = await _eval.ListCandidateTeamsAsync(default);

        Assert.IsEmpty(teams);
    }

    // ----- SelectCandidateAsync: pad windows -----

    [TestMethod]
    public async Task Select_BeforePrePad_ReturnsNull()
    {
        // Race at 10:00 wall (15:00 UTC for America/Chicago in June DST). Pre-pad is 30 min.
        AddRace(teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00), durationHours: 2, redMistEventId: 100);
        await _db.SaveChangesAsync();

        // now = 09:00 wall = 60 min before start, before the 30-min pre-pad window.
        var nowUtc = WallToUtc(Wall(2026, 6, 1, 9, 00));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNull(c);
    }

    [TestMethod]
    public async Task Select_WithinPrePad_ReturnsCandidate_InWindowTrue()
    {
        AddRace(teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00), durationHours: 2, redMistEventId: 100);
        await _db.SaveChangesAsync();

        // 15 min before start: inside the 30-min pre-pad.
        var nowUtc = WallToUtc(Wall(2026, 6, 1, 9, 45));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNotNull(c);
        Assert.AreEqual(100, c.RedMistEventId);
        Assert.IsTrue(c.InWindow);
    }

    [TestMethod]
    public async Task Select_DuringRace_InWindowTrue()
    {
        AddRace(teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00), durationHours: 2, redMistEventId: 100);
        await _db.SaveChangesAsync();

        var nowUtc = WallToUtc(Wall(2026, 6, 1, 11, 00));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNotNull(c);
        Assert.IsTrue(c.InWindow);
    }

    [TestMethod]
    public async Task Select_WithinPostPad_InWindowTrue()
    {
        AddRace(teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00), durationHours: 2, redMistEventId: 100);
        await _db.SaveChangesAsync();

        // 15 min after end (12:00 + 15min). Inside the 30-min post-pad.
        var nowUtc = WallToUtc(Wall(2026, 6, 1, 12, 15));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNotNull(c);
        Assert.IsTrue(c.InWindow);
    }

    [TestMethod]
    public async Task Select_JustOutsidePostPad_InWindowFalseButCandidateReturned()
    {
        // The activation rule pushes "out-of-window" handling to the caller (IsLive extension
        // could still keep the lease alive). The evaluator surfaces the candidate with
        // InWindow=false instead of dropping it.
        AddRace(teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00), durationHours: 2, redMistEventId: 100);
        await _db.SaveChangesAsync();

        // 45 min after end, past the 30-min post-pad but well inside the 12 h hard cutoff.
        var nowUtc = WallToUtc(Wall(2026, 6, 1, 12, 45));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNotNull(c);
        Assert.IsFalse(c.InWindow);
    }

    [TestMethod]
    public async Task Select_BeyondHardCutoff_ReturnsNull()
    {
        AddRace(teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00), durationHours: 2, redMistEventId: 100);
        await _db.SaveChangesAsync();

        // 13 hours after end — past the 12 h post-pad cutoff defined in the evaluator.
        var nowUtc = WallToUtc(Wall(2026, 6, 2, 1, 30));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNull(c);
    }

    // ----- Filtering and tie-break -----

    [TestMethod]
    public async Task Select_SkipsRacesWithoutEventOrOrganization()
    {
        // Race has neither RedMistEventId nor RedMistOrganizationId — not paired to RedMist
        // at all, so it should not produce a candidate.
        AddRace(teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00), redMistEventId: null);
        await _db.SaveChangesAsync();

        var nowUtc = WallToUtc(Wall(2026, 6, 1, 10, 00));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNull(c);
    }

    [TestMethod]
    public async Task Select_OrgOnlyRow_ReturnsCandidateWithOrgId_AndNullEventId()
    {
        // Org-only Race: no explicit event id, but the org is set. Evaluator returns it as
        // a candidate; the worker's resolver fills in EventId from RedMist live-events.
        AddRace(teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00),
            redMistEventId: null, redMistOrganizationId: 42);
        await _db.SaveChangesAsync();

        var nowUtc = WallToUtc(Wall(2026, 6, 1, 10, 30));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNotNull(c);
        Assert.IsNull(c.RedMistEventId);
        Assert.AreEqual(42, c.RedMistOrganizationId);
        Assert.IsTrue(c.InWindow);
    }

    [TestMethod]
    public async Task ListCandidateTeams_IncludesOrgOnlyTeams()
    {
        AddRace(teamId: 10, startWallClock: Wall(2026, 6, 1, 10, 00), redMistEventId: 100);
        AddRace(teamId: 20, startWallClock: Wall(2026, 6, 1, 10, 00),
            redMistEventId: null, redMistOrganizationId: 99);
        await _db.SaveChangesAsync();

        var teams = await _eval.ListCandidateTeamsAsync(default);

        CollectionAssert.AreEquivalent(new[] { 10, 20 }, teams.ToArray());
    }

    // ----- Explicit selection (Team.SelectedRaceId) -----

    [TestMethod]
    public async Task Select_HonorsExplicitSelection_OverTimeWindowAutoPick()
    {
        // Race A (id 1) is closer to "now" by the time-window rule, but the team has
        // explicitly selected Race B (id 2) — the explicit choice must win.
        AddRace(id: 1, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00), redMistEventId: 100);
        AddRace(id: 2, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 14, 00), redMistEventId: 200);
        AddTeam(id: TeamId, selectedRaceId: 2);
        await _db.SaveChangesAsync();

        var nowUtc = WallToUtc(Wall(2026, 6, 1, 10, 30));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNotNull(c);
        Assert.AreEqual(2, c.RaceId);
        Assert.AreEqual(200, c.RedMistEventId);
    }

    [TestMethod]
    public async Task Select_ExplicitSelection_OutOfWindow_ReturnsCandidateWithInWindowFalse()
    {
        // User picked a future race. Activation evaluator returns the candidate but flags
        // InWindow=false so the lease decision treats it as "no attach yet".
        AddRace(id: 1, teamId: TeamId, startWallClock: Wall(2026, 6, 5, 10, 00), redMistEventId: 100);
        AddTeam(id: TeamId, selectedRaceId: 1);
        await _db.SaveChangesAsync();

        var nowUtc = WallToUtc(Wall(2026, 6, 1, 10, 00));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNotNull(c);
        Assert.AreEqual(1, c.RaceId);
        Assert.IsFalse(c.InWindow);
    }

    [TestMethod]
    public async Task Select_ExplicitSelection_UnpairedRace_ReturnsNull_NoFallback()
    {
        // Picking an unpaired race is an explicit "stop monitoring RedMist" — even if
        // another race is paired and in window, the user's choice wins. Returns null so
        // the worker detaches; falling back to the paired race would silently override
        // the user's intent.
        AddRace(id: 1, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00), redMistEventId: 100);
        AddRace(id: 2, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 11, 00), redMistEventId: null);
        AddTeam(id: TeamId, selectedRaceId: 2);
        await _db.SaveChangesAsync();

        var nowUtc = WallToUtc(Wall(2026, 6, 1, 10, 30));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNull(c);
    }

    [TestMethod]
    public async Task Select_ExplicitSelection_MissingRace_ReturnsNull_NoFallback()
    {
        // Selection points at a race that doesn't exist (drift — DeleteRace should have
        // cleared it but didn't). Don't silently switch to another race; return null so
        // the user gets a blank header and can re-pick from the dropdown.
        AddRace(id: 1, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00), redMistEventId: 100);
        AddTeam(id: TeamId, selectedRaceId: 999);
        await _db.SaveChangesAsync();

        var nowUtc = WallToUtc(Wall(2026, 6, 1, 10, 30));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNull(c);
    }

    [TestMethod]
    public async Task Select_AccessCode_FlowsFromRaceToCandidate()
    {
        // Private-event access code carries through to ActivationCandidate so the worker
        // can pass it to SubscribeToEventV2WithCode. Tested separately from event-id
        // selection so a regression in the projection wouldn't be masked by the other
        // happy-path tests (which leave AccessCode null).
        AddRace(id: 1, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00),
            redMistEventId: 100, redMistAccessCode: "ABC123");
        await _db.SaveChangesAsync();

        var nowUtc = WallToUtc(Wall(2026, 6, 1, 10, 30));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNotNull(c);
        Assert.AreEqual("ABC123", c.RedMistAccessCode);
    }

    [TestMethod]
    public async Task ListCandidateTeams_IncludesTeamsWithSelectionButNoPairedRaces()
    {
        // Edge case: team has a SelectedRaceId set, but no Race row currently has a
        // RedMistEventId/OrgId (perhaps the user-selected race is itself an unpaired one).
        // The team still needs to appear in the candidate list so SelectCandidateAsync can
        // run and either return the selected candidate or fall back.
        AddRace(id: 1, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00), redMistEventId: null);
        AddTeam(id: TeamId, selectedRaceId: 1);
        await _db.SaveChangesAsync();

        var teams = await _eval.ListCandidateTeamsAsync(default);

        Assert.Contains(TeamId, teams);
    }

    [TestMethod]
    public async Task Select_OtherTeamsRacesAreIgnored()
    {
        AddRace(teamId: 99, startWallClock: Wall(2026, 6, 1, 10, 00), redMistEventId: 999);
        await _db.SaveChangesAsync();

        var nowUtc = WallToUtc(Wall(2026, 6, 1, 10, 00));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNull(c);
    }

    [TestMethod]
    public async Task Select_PicksRaceClosestToNow()
    {
        AddRace(id: 1, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 9, 00), redMistEventId: 100);
        AddRace(id: 2, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 11, 00), redMistEventId: 200);
        AddRace(id: 3, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 12, 00), redMistEventId: 300);
        await _db.SaveChangesAsync();

        // now = 10:30 — closer to the 11:00 race than to the 9:00 race.
        var nowUtc = WallToUtc(Wall(2026, 6, 1, 10, 30));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNotNull(c);
        Assert.AreEqual(200, c.RedMistEventId);
    }

    [TestMethod]
    public async Task Select_TieBreakByRaceId_WhenEqualDistance()
    {
        // Two races equidistant from now: id 1 at 9:00, id 2 at 11:00, now = 10:00.
        AddRace(id: 1, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 9, 00), redMistEventId: 100);
        AddRace(id: 2, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 11, 00), redMistEventId: 200);
        await _db.SaveChangesAsync();

        var nowUtc = WallToUtc(Wall(2026, 6, 1, 10, 00));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNotNull(c);
        Assert.AreEqual(1, c.RaceId);
        Assert.AreEqual(100, c.RedMistEventId);
    }

    // ----- TZ handling -----

    [TestMethod]
    public async Task Select_RespectsTimeZone_PerRace()
    {
        // Race scheduled 10:00 wall in Tokyo (UTC+9 in June, no DST).
        AddRace(id: 1, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00),
            durationHours: 2, redMistEventId: 100, timeZone: "Asia/Tokyo");
        await _db.SaveChangesAsync();

        // Tokyo 10:00 == 01:00 UTC. Test now = 01:30 UTC → 30 min into the race.
        var nowUtc = new DateTime(2026, 6, 1, 1, 30, 0, DateTimeKind.Utc);

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNotNull(c);
        Assert.IsTrue(c.InWindow);
    }

    [TestMethod]
    public async Task Select_SkipsRaceWithUnknownTimeZone()
    {
        AddRace(id: 1, teamId: TeamId, startWallClock: Wall(2026, 6, 1, 10, 00),
            redMistEventId: 100, timeZone: "Definitely/NotARealZone");
        await _db.SaveChangesAsync();

        var nowUtc = WallToUtc(Wall(2026, 6, 1, 10, 30));

        var c = await _eval.SelectCandidateAsync(TeamId, nowUtc, default);

        Assert.IsNull(c);
    }

    // ----- LoadTeamCarNumbersAsync -----

    [TestMethod]
    public async Task LoadTeamCarNumbers_ReturnsTeamCarsCaseInsensitive()
    {
        AddCarConfig(teamId: TeamId, car: "42");
        AddCarConfig(teamId: TeamId, car: "5");
        AddCarConfig(teamId: 99, car: "1"); // other team's car
        await _db.SaveChangesAsync();

        var numbers = await _eval.LoadTeamCarNumbersAsync(TeamId, default);

        Assert.HasCount(2, numbers);
        Assert.Contains("42", numbers);
        Assert.Contains("5", numbers);
        // Comparer is case-insensitive so letter-suffix car numbers ("12A" vs "12a") collapse.
        Assert.Contains("42", numbers);
    }

    [TestMethod]
    public async Task LoadTeamCarNumbers_EmptyWhenNoConfigs()
    {
        var numbers = await _eval.LoadTeamCarNumbersAsync(TeamId, default);
        Assert.IsEmpty(numbers);
    }

    // ----- helpers -----

    private static DateTime Wall(int y, int M, int d, int h, int m) => new(y, M, d, h, m, 0, DateTimeKind.Unspecified);

    private static DateTime WallToUtc(DateTime wall, string tz = Tz)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(tz);
        return TimeZoneInfo.ConvertTimeToUtc(wall, zone);
    }

    private void AddRace(
        int teamId,
        DateTime startWallClock,
        double durationHours = 2,
        int? redMistEventId = null,
        int? redMistOrganizationId = null,
        string? redMistAccessCode = null,
        string timeZone = Tz,
        int id = 0)
    {
        _db.Races.Add(new Race
        {
            Id = id,
            TeamId = teamId,
            Name = "test",
            Start = startWallClock,
            Duration = durationHours,
            TimeZone = timeZone,
            RedMistEventId = redMistEventId,
            RedMistOrganizationId = redMistOrganizationId,
            RedMistAccessCode = redMistAccessCode,
        });
    }

    private void AddTeam(int id, int? selectedRaceId = null)
    {
        _db.Teams.Add(new Team
        {
            Id = id,
            Name = $"team-{id}",
            ClientId = $"client-{id}",
            SelectedRaceId = selectedRaceId,
        });
    }

    private void AddCarConfig(int teamId, string car)
    {
        _db.CarConfigurations.Add(new CarConfigurationTable
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            Car = car,
            Name = $"car-{car}",
            ConfigurationSchemaVersion = 1,
            ConfigurationJson = "{}",
            LastUpdated = DateTime.UtcNow,
        });
    }
}
