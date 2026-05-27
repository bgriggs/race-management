using System.Text.Json;
using System.Text.RegularExpressions;
using Channels;
using Channels.Logic;
using Cloud.Shared;
using Cloud.Shared.Alarms;
using Cloud.Shared.Auth;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models;
using Cloud.Shared.Database.Models.Alarms;
using Common;
using MessagePack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace WebApi.Controllers;


[Route("v{version:apiVersion}/[controller]/[action]")]
[ApiVersion("1.0")]
[Produces("application/json")]
public partial class ConfigurationController(
    ITeamRoleContext teamRoleContext,
    IDbContextFactory<RaceManagementContext> dbFactory,
    IConnectionMultiplexer redis,
    IRedisAlarmStateGateway alarmStateGateway,
    IActiveAlarmsReader activeAlarmsReader,
    TimeProvider timeProvider,
    ILogger<ConfigurationController> logger) : Controller
{
    #region Car Configurations

    [HttpGet]
    [ProducesResponseType<CarConfiguration>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarConfiguration>> LoadCarConfigurationByCarAsync(int teamId, string carNumber, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.CarConfigurations
            .AsNoTracking()
            .Where(c => c.TeamId == teamId && c.Car == carNumber)
            .OrderByDescending(c => c.LastUpdated)
            .FirstOrDefaultAsync(ct);

        if (row is null) { return NotFound(); }
        return Deserialize(row);
    }

    [HttpGet]
    [ProducesResponseType<CarConfiguration>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarConfiguration>> LoadCarConfigurationByIdAsync(int teamId, Guid configurationId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.CarConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == configurationId && c.TeamId == teamId, ct);

        if (row is null) { return NotFound(); }
        return Deserialize(row);
    }

    [HttpPost]
    [ProducesResponseType<SaveCarConfigurationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SaveCarConfigurationAsync(int teamId, [FromBody] CarConfiguration configuration, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamRoleAsync(teamId, "admin", ct)) { return Forbid(); }

        if (configuration.ConfigurationId == Guid.Empty)
        {
            ModelState.AddModelError(nameof(configuration.ConfigurationId), "ConfigurationId is required and cannot be Guid.Empty.");
            return ValidationProblem(ModelState);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var id = configuration.ConfigurationId;
        var now = DateTime.UtcNow;

        var existing = await db.CarConfigurations.FirstOrDefaultAsync(c => c.Id == id && c.TeamId == teamId, ct);
        var prior = existing is null ? null : Deserialize(existing);

        var routingErrors = ValidateChannelRouting(configuration, prior);
        if (routingErrors.Count > 0)
        {
            foreach (var err in routingErrors)
            {
                ModelState.AddModelError(nameof(configuration.ChannelDefinitions), err);
            }
            return ValidationProblem(ModelState);
        }

        var json = JsonSerializer.Serialize(configuration);

        if (existing is null)
        {
            db.CarConfigurations.Add(new CarConfigurationTable
            {
                Id = id,
                TeamId = teamId,
                Car = configuration.Car,
                Name = configuration.Name,
                Notes = configuration.Notes,
                ConfigurationSchemaVersion = configuration.ConfigurationSchemaVersion,
                LastUpdated = configuration.LastUpdated,
                LastUpdatedOnCarTimestamp = configuration.LastUpdatedOnCarTimestamp,
                ConfigurationJson = json,
            });
        }
        else
        {
            existing.Car = configuration.Car;
            existing.Name = configuration.Name;
            existing.Notes = configuration.Notes;
            existing.ConfigurationSchemaVersion = configuration.ConfigurationSchemaVersion;
            existing.LastUpdated = now;
            existing.LastUpdatedOnCarTimestamp = configuration.LastUpdatedOnCarTimestamp;
            existing.ConfigurationJson = json;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new SaveCarConfigurationResult(id, now));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCarConfigurationAsync(int teamId, Guid configurationId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamRoleAsync(teamId, "admin", ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.CarConfigurations
            .Where(c => c.Id == configurationId && c.TeamId == teamId)
            .ExecuteDeleteAsync(ct);

        return rows == 0 ? NotFound() : NoContent();
    }

    #endregion

    #region Teams

    [HttpPost]
    [ProducesResponseType<Team>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateTeamAsync([FromBody] TeamRequest request, CancellationToken ct)
    {
        if (!User.IsInRole("system-admin")) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var team = new Team
        {
            Name = request.Name,
            ClientId = request.ClientId,
        };
        db.Teams.Add(team);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetTeamAsync), new { teamId = team.Id }, team);
    }

    [HttpGet]
    [ProducesResponseType<List<UserTeam>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<UserTeam>>> ListMyTeamsAsync(CancellationToken ct)
    {
        var memberships = await teamRoleContext.GetUserTeamsAsync(ct);
        if (memberships.Count == 0) { return new List<UserTeam>(); }

        var teamIds = memberships.Select(m => m.TeamId).ToHashSet();
        var roleByTeam = memberships.ToDictionary(m => m.TeamId, m => m.Roles);

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var teams = await db.Teams
            .AsNoTracking()
            .Where(t => teamIds.Contains(t.Id) && !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return teams
            .Select(t => new UserTeam(t.Id, t.Name, t.ClientId, roleByTeam[t.Id]))
            .ToList();
    }

    [HttpGet]
    [ProducesResponseType<List<Team>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<Team>>> LoadTeamsAsync(CancellationToken ct)
    {
        if (!User.IsInRole("system-admin")) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Teams
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
    }

    [HttpGet]
    [ProducesResponseType<Team>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Team>> GetTeamAsync(int teamId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var team = await db.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted, ct);

        return team is null ? NotFound() : team;
    }

    [HttpPost]
    [ProducesResponseType<Team>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Team>> UpdateTeamAsync(int teamId, [FromBody] TeamRequest request, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamRoleAsync(teamId, "admin", ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted, ct);
        if (team is null) { return NotFound(); }

        team.Name = request.Name;
        team.ClientId = request.ClientId;
        await db.SaveChangesAsync(ct);
        return team;
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTeamAsync(int teamId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamRoleAsync(teamId, "admin", ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId && !t.IsDeleted, ct);
        if (team is null) { return NotFound(); }

        team.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    #endregion

    #region Cars

    [HttpPost]
    [ProducesResponseType<Car>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCarAsync(int teamId, [FromBody] CarRequest request, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamRoleAsync(teamId, "admin", ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = await db.Cars.FirstOrDefaultAsync(c => c.TeamId == teamId && c.Number == request.Number, ct);
        if (existing is not null)
        {
            if (!existing.IsDeleted) { return Conflict($"Car '{request.Number}' already exists for team {teamId}."); }
            existing.Make = request.Make;
            existing.Model = request.Model;
            existing.Color = request.Color;
            existing.IsDeleted = false;
            await db.SaveChangesAsync(ct);
            return CreatedAtAction(nameof(GetCarAsync), new { teamId, carNumber = existing.Number }, existing);
        }

        var car = new Car
        {
            TeamId = teamId,
            Number = request.Number,
            Make = request.Make,
            Model = request.Model,
            Color = request.Color,
        };
        db.Cars.Add(car);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetCarAsync), new { teamId, carNumber = car.Number }, car);
    }

    [HttpGet]
    [ProducesResponseType<Car>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Car>> GetCarAsync(int teamId, string carNumber, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var car = await db.Cars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TeamId == teamId && c.Number == carNumber && !c.IsDeleted, ct);

        return car is null ? NotFound() : car;
    }

    [HttpGet]
    [ProducesResponseType<List<Car>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<Car>>> ListCarsAsync(int teamId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Cars
            .AsNoTracking()
            .Where(c => c.TeamId == teamId && !c.IsDeleted)
            .OrderBy(c => c.Number)
            .ToListAsync(ct);
    }

    [HttpPost]
    [ProducesResponseType<Car>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Car>> UpdateCarAsync(int teamId, string carNumber, [FromBody] CarUpdate request, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamRoleAsync(teamId, "admin", ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var car = await db.Cars.FirstOrDefaultAsync(c => c.TeamId == teamId && c.Number == carNumber && !c.IsDeleted, ct);
        if (car is null) { return NotFound(); }

        car.Make = request.Make;
        car.Model = request.Model;
        car.Color = request.Color;
        await db.SaveChangesAsync(ct);
        return car;
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCarAsync(int teamId, string carNumber, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamRoleAsync(teamId, "admin", ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var car = await db.Cars.FirstOrDefaultAsync(c => c.TeamId == teamId && c.Number == carNumber && !c.IsDeleted, ct);
        if (car is null) { return NotFound(); }

        car.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static CarConfiguration Deserialize(CarConfigurationTable row)
    {
        var config = JsonSerializer.Deserialize<CarConfiguration>(row.ConfigurationJson)
            ?? throw new InvalidOperationException($"Failed to deserialize CarConfiguration {row.Id}");
        config.ConfigurationId = row.Id;
        return config;
    }

    /// <summary>
    /// Enforces channel routing invariants on save (ADR-0007 amendment 2026-05-25):
    ///   1. Reserved channels whose template has IsDistributionLocked=true must keep the template's Distribution.
    ///   2. A channel's Distribution origin family (Car* vs Cloud*) is fixed at creation and cannot change on edit.
    /// The UI also enforces both, but a non-UI client (or a buggy one) could otherwise write invalid state.
    /// </summary>
    public static List<string> ValidateChannelRouting(CarConfiguration incoming, CarConfiguration? prior)
    {
        var errors = new List<string>();
        var priorById = prior?.ChannelDefinitions.ToDictionary(c => c.Id) ?? [];
        var reservedTemplatesById = ReservedChannels.Channels.ToDictionary(c => c.Id);

        foreach (var ch in incoming.ChannelDefinitions)
        {
            if (reservedTemplatesById.TryGetValue(ch.Id, out var template)
                && template.IsDistributionLocked
                && ch.Distribution != template.Distribution)
            {
                errors.Add(
                    $"Channel '{ch.Name}' ({ch.Id}): Distribution is locked to {template.Distribution} " +
                    $"by feature '{template.ManagedByFeature}'; cannot be saved as {ch.Distribution}.");
            }

            if (priorById.TryGetValue(ch.Id, out var prev)
                && OriginOf(ch.Distribution) != OriginOf(prev.Distribution))
            {
                errors.Add(
                    $"Channel '{ch.Name}' ({ch.Id}): Origin is fixed at creation; cannot change from " +
                    $"{prev.Distribution} ({OriginOf(prev.Distribution)}-origin) to {ch.Distribution} ({OriginOf(ch.Distribution)}-origin).");
            }
        }

        return errors;
    }

    private static string OriginOf(ChannelDistribution d) =>
        d == ChannelDistribution.CarLocal || d == ChannelDistribution.CarToCloud ? "Car" : "Cloud";

    #endregion

    #region Races

    [HttpGet]
    [ProducesResponseType<Race>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Race>> LoadRaceAsync(int teamId, int raceId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var race = await db.Races
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == raceId && r.TeamId == teamId, ct);

        return race is null ? NotFound() : race;
    }

    [HttpGet]
    [ProducesResponseType<List<Race>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<Race>>> ListRacesAsync(int teamId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Races
            .AsNoTracking()
            .Where(r => r.TeamId == teamId)
            .OrderByDescending(r => r.Start)
            .ToListAsync(ct);
    }

    [HttpPost]
    [ProducesResponseType<Race>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Race>> SaveRaceAsync(int teamId, [FromBody] Race race, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamRoleAsync(teamId, "admin", ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        if (race.Id == 0)
        {
            race.TeamId = teamId;
            db.Races.Add(race);
            await db.SaveChangesAsync(ct);
            await PublishTeamRaceChangedAsync(teamId);
            return race;
        }

        var existing = await db.Races.FirstOrDefaultAsync(r => r.Id == race.Id && r.TeamId == teamId, ct);
        if (existing is null) { return NotFound(); }

        existing.Name = race.Name;
        existing.Start = race.Start;
        existing.Duration = race.Duration;
        existing.TimeZone = race.TimeZone;
        existing.Notes = race.Notes;
        existing.RedMistEventId = race.RedMistEventId;
        existing.RedMistOrganizationId = race.RedMistOrganizationId;
        existing.RedMistAccessCode = race.RedMistAccessCode;
        await db.SaveChangesAsync(ct);
        await PublishTeamRaceChangedAsync(teamId);
        return existing;
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRaceAsync(int teamId, int raceId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamRoleAsync(teamId, "admin", ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // If the team had this race selected, clear the selection so the worker falls back
        // to the time-window auto-pick rather than holding a stale reference.
        await db.Teams
            .Where(t => t.Id == teamId && t.SelectedRaceId == raceId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SelectedRaceId, (int?)null), ct);

        var rows = await db.Races
            .Where(r => r.Id == raceId && r.TeamId == teamId)
            .ExecuteDeleteAsync(ct);

        if (rows == 0) return NotFound();
        await PublishTeamRaceChangedAsync(teamId);
        return NoContent();
    }

    /// <summary>
    /// Sets the team's currently-monitored race. Drives the ChannelProcessor's RedMist
    /// subscription via <see cref="Team.SelectedRaceId"/>; the activation evaluator honors
    /// this when set and falls back to the time-window auto-pick when cleared. Pass
    /// <paramref name="raceId"/> as <c>null</c> to clear. Any team member can change this —
    /// the dropdown in the Race-Monitor header is the primary caller.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SelectRaceAsync(int teamId, int? raceId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Validate the Race exists and belongs to this team — prevents pointing a team at
        // another team's race or a deleted one.
        if (raceId is int rid)
        {
            var exists = await db.Races.AnyAsync(r => r.Id == rid && r.TeamId == teamId, ct);
            if (!exists) return NotFound();
        }

        var updated = await db.Teams
            .Where(t => t.Id == teamId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.SelectedRaceId, raceId), ct);
        if (updated == 0) return NotFound();

        await PublishTeamRaceChangedAsync(teamId);
        return NoContent();
    }

    private async Task PublishTeamRaceChangedAsync(int teamId)
    {
        try
        {
            var sub = redis.GetSubscriber();
            var channel = RedisChannel.Literal(string.Format(Consts.TEAM_RACE_CHANGED_CHANNEL, teamId));
            await sub.PublishAsync(channel, RedisValue.EmptyString);
        }
        catch (Exception ex)
        {
            // Pub/sub is a wake-up hint; the worker's 30s tick will pick up the change
            // even if this publish fails. Don't fail the user's request over it.
            logger.LogWarning(ex, "Failed to publish team-race-changed for team {TeamId}", teamId);
        }
    }

    #endregion

    #region Channel Status Table

    [HttpGet]
    public async Task<ActionResult<ChannelStatusTableConfiguration>> LoadChannelStatusTableConfigurationAsync(int teamId, string userId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var config = await db.ChannelStatusTableConfigurations
            .AsNoTracking()
            .Include(c => c.Columns)
            .FirstOrDefaultAsync(c => c.TeamId == teamId && c.UserId == userId, ct);

        if (config is null) { return NotFound(); }
        return config;
    }

    [HttpPost]
    public async Task<IActionResult> SaveChannelStatusTableConfigurationAsync(int teamId, string userId, [FromBody] ChannelStatusTableConfiguration configuration, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        foreach (var column in configuration.Columns)
        {
            column.TeamId = teamId;
            column.UserId = userId;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var existing = await db.ChannelStatusTableConfigurations
            .Include(c => c.Columns)
            .FirstOrDefaultAsync(c => c.TeamId == teamId && c.UserId == userId, ct);
        if (existing is null)
        {
            configuration.TeamId = teamId;
            configuration.UserId = userId;
            db.ChannelStatusTableConfigurations.Add(configuration);
        }
        else
        {
            db.RemoveRange(existing.Columns);
            existing.Columns = configuration.Columns;
        }

        await db.SaveChangesAsync(ct);
        return Ok();
    }

    #endregion

    #region Site Settings

    [HttpGet]
    [ProducesResponseType<SiteSettings>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SiteSettings>> LoadSiteSettingsAsync(int teamId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamRoleAsync(teamId, "admin", ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var settings = await db.SiteSettings
            .AsNoTracking()
            .Where(s => s.TeamId == teamId)
            .Select(s => new SiteSettings
            {
                TeamId = s.TeamId,
                RedMistClientId = s.RedMistClientId,
            })
            .FirstOrDefaultAsync(ct);

        return settings is null ? NotFound() : settings;
    }

    [HttpPost]
    [ProducesResponseType<SiteSettings>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SiteSettings>> SaveSiteSettingsAsync(int teamId, [FromBody] SiteSettings settings, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamRoleAsync(teamId, "admin", ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var existing = await db.SiteSettings.FirstOrDefaultAsync(s => s.TeamId == teamId, ct);
        if (existing is null)
        {
            settings.TeamId = teamId;
            db.SiteSettings.Add(settings);
            await db.SaveChangesAsync(ct);
            return settings;
        }

        existing.RedMistClientId = settings.RedMistClientId;
        if (!string.IsNullOrEmpty(settings.RedMistClientSecret))
        {
            existing.RedMistClientSecret = settings.RedMistClientSecret;
        }
        await db.SaveChangesAsync(ct);
        return existing;
    }

    #endregion

    #region Alarms

    [HttpGet]
    [ProducesResponseType<AlarmDefinitionDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AlarmDefinitionDto[]>> LoadAlarmDefinitionsAsync(int teamId, string? carNumber, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // carNumber null → only team-level rows. carNumber set → team-level ∪ that car's rows.
        var rows = await db.AlarmDefinitions
            .AsNoTracking()
            .Where(a => a.TeamId == teamId &&
                (carNumber == null
                    ? a.CarNumber == null
                    : a.CarNumber == null || a.CarNumber == carNumber))
            .OrderBy(a => a.CarNumber == null ? 0 : 1).ThenBy(a => a.Name)
            .ToListAsync(ct);

        return rows.Select(ToDto).ToArray();
    }

    [HttpGet]
    [ProducesResponseType<AlarmDefinitionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlarmDefinitionDto>> LoadAlarmDefinitionAsync(int teamId, Guid alarmId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.AlarmDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == alarmId && a.TeamId == teamId, ct);

        return row is null ? NotFound() : ToDto(row);
    }

    [HttpPost]
    [ProducesResponseType<AlarmDefinitionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SaveAlarmDefinitionAsync(int teamId, [FromBody] AlarmDefinitionDto definition, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamRoleAsync(teamId, "admin", ct)) { return Forbid(); }

        if (!ValidateAlarmDefinition(definition, ModelState)) { return ValidationProblem(ModelState); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var existing = definition.Id == Guid.Empty
            ? null
            : await db.AlarmDefinitions.FirstOrDefaultAsync(a => a.Id == definition.Id && a.TeamId == teamId, ct);

        var statementJson = JsonSerializer.Serialize(definition.Statement);

        AlarmDefinitionRow row;
        if (existing is null)
        {
            row = new AlarmDefinitionRow
            {
                Id = definition.Id == Guid.Empty ? Guid.NewGuid() : definition.Id,
                TeamId = teamId,
                CarNumber = definition.CarNumber,
                Name = definition.Name,
                Message = definition.Message,
                DisplayChannelSourceColorHex = definition.DisplayChannelSourceColorHex,
                TimeAfterAckToDisplaySecs = definition.TimeAfterAckToDisplaySecs,
                AlarmStatusChannelId = definition.AlarmStatusChannelId,
                StatementJson = statementJson,
            };
            db.AlarmDefinitions.Add(row);
        }
        else
        {
            // Reject scope changes on update — delete+create instead so history isn't confused.
            if (existing.CarNumber != definition.CarNumber)
            {
                ModelState.AddModelError(nameof(definition.CarNumber),
                    "Cannot change CarNumber on an existing alarm; delete and recreate to move scope.");
                return ValidationProblem(ModelState);
            }

            existing.Name = definition.Name;
            existing.Message = definition.Message;
            existing.DisplayChannelSourceColorHex = definition.DisplayChannelSourceColorHex;
            existing.TimeAfterAckToDisplaySecs = definition.TimeAfterAckToDisplaySecs;
            existing.AlarmStatusChannelId = definition.AlarmStatusChannelId;
            existing.StatementJson = statementJson;
            row = existing;
        }

        await db.SaveChangesAsync(ct);
        await PublishAlarmConfigChangedAsync(teamId);

        return Ok(ToDto(row));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAlarmDefinitionAsync(int teamId, Guid alarmId, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamRoleAsync(teamId, "admin", ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.AlarmDefinitions
            .Where(a => a.Id == alarmId && a.TeamId == teamId)
            .ExecuteDeleteAsync(ct);

        if (rows == 0) return NotFound();

        await PublishAlarmConfigChangedAsync(teamId);
        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType<ActiveAlarmDto[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ActiveAlarmDto[]>> LoadActiveAlarmsAsync(int teamId, bool includeAcknowledged, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }
        return await activeAlarmsReader.GetForTeamAsync(teamId, includeAcknowledged, ct);
    }

    [HttpPost]
    [ProducesResponseType<ActiveAlarmDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActiveAlarmDto>> AcknowledgeAlarmAsync(int teamId, string carNumber, Guid alarmId, CancellationToken ct)
    {
        // Race-day ack: any team member can ack; admin-only would create a bottleneck.
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.ActiveAlarms.FirstOrDefaultAsync(
            a => a.TeamId == teamId && a.CarNumber == carNumber && a.AlarmDefinitionId == alarmId, ct);
        if (row is null) return NotFound();

        // Idempotent: re-ack leaves the existing timestamp and skips the event row.
        if (row.IsAcknowledged) { return await BuildActiveAlarmDtoAsync(db, row, ct); }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var carKey = string.Format(Consts.CAR_STREAM_FIELD, teamId, carNumber);

        // ADR-0002 ordering — all writes complete before the response.
        await alarmStateGateway.AcknowledgeAsync(carKey, alarmId, now, ct);

        row.IsAcknowledged = true;
        row.LastAcknowledgedTimestamp = now;

        db.AlarmEvents.Add(new AlarmEventRow
        {
            TeamId = teamId,
            CarNumber = carNumber,
            AlarmDefinitionId = alarmId,
            EventType = AlarmEventType.Acknowledged,
            Timestamp = now,
        });

        await db.SaveChangesAsync(ct);

        await PublishAlarmChangeAsync(new AlarmChangeNotification
        {
            TeamId = teamId,
            CarNumber = carNumber,
            AlarmDefinitionId = alarmId,
            EventType = AlarmEventType.Acknowledged,
            IsActive = row.IsActive,
            IsAcknowledged = true,
            Timestamp = now,
        });

        return await BuildActiveAlarmDtoAsync(db, row, ct);
    }

    private static AlarmDefinitionDto ToDto(AlarmDefinitionRow row)
    {
        StatementDefinition statement;
        try
        {
            statement = string.IsNullOrWhiteSpace(row.StatementJson)
                ? new StatementDefinition()
                : JsonSerializer.Deserialize<StatementDefinition>(row.StatementJson) ?? new StatementDefinition();
        }
        catch (JsonException)
        {
            statement = new StatementDefinition();
        }

        return new AlarmDefinitionDto
        {
            Id = row.Id,
            TeamId = row.TeamId,
            CarNumber = row.CarNumber,
            Name = row.Name,
            Message = row.Message,
            DisplayChannelSourceColorHex = row.DisplayChannelSourceColorHex,
            TimeAfterAckToDisplaySecs = row.TimeAfterAckToDisplaySecs,
            AlarmStatusChannelId = row.AlarmStatusChannelId,
            Statement = statement,
        };
    }

    private static async Task<ActiveAlarmDto> BuildActiveAlarmDtoAsync(RaceManagementContext db, ActiveAlarmRow row, CancellationToken ct)
    {
        var def = await db.AlarmDefinitions.AsNoTracking().FirstAsync(d => d.Id == row.AlarmDefinitionId, ct);
        return new ActiveAlarmDto
        {
            TeamId = row.TeamId,
            CarNumber = row.CarNumber,
            AlarmDefinitionId = row.AlarmDefinitionId,
            Name = def.Name,
            Message = def.Message,
            DisplayChannelSourceColorHex = def.DisplayChannelSourceColorHex,
            TimeAfterAckToDisplaySecs = def.TimeAfterAckToDisplaySecs,
            IsActive = row.IsActive,
            IsAcknowledged = row.IsAcknowledged,
            LastActivatedAt = row.LastActivatedAt,
            LastAcknowledgedTimestamp = row.LastAcknowledgedTimestamp,
        };
    }

    public static bool ValidateAlarmDefinition(AlarmDefinitionDto definition, ModelStateDictionary modelState)
    {
        if (string.IsNullOrWhiteSpace(definition.Name) || definition.Name.Length > 20)
            modelState.AddModelError(nameof(definition.Name), "Name is required and must be 1-20 characters.");

        if (!HexColorRegex().IsMatch(definition.DisplayChannelSourceColorHex))
            modelState.AddModelError(nameof(definition.DisplayChannelSourceColorHex), "DisplayChannelSourceColorHex must be #RRGGBB.");

        if (definition.TimeAfterAckToDisplaySecs < 0)
            modelState.AddModelError(nameof(definition.TimeAfterAckToDisplaySecs), "TimeAfterAckToDisplaySecs cannot be negative.");

        var statement = definition.Statement;
        if (statement is null || statement.ActivateComparisons is null || statement.ActivateComparisons.Count == 0
            || statement.ActivateComparisons.All(group => group is null || group.Count == 0))
        {
            modelState.AddModelError($"{nameof(definition.Statement)}.{nameof(StatementDefinition.ActivateComparisons)}",
                "At least one ActivateComparison row with at least one comparison is required.");
        }
        else
        {
            foreach (var group in statement.ActivateComparisons)
            {
                if (group is null) continue;
                foreach (var comparison in group)
                {
                    if (comparison.Id == Guid.Empty)
                        modelState.AddModelError(nameof(ComparisonDefinition.Id), "Every comparison must have a non-empty Id.");
                    if (comparison.ChannelId == Guid.Empty)
                        modelState.AddModelError(nameof(ComparisonDefinition.ChannelId), "Every comparison must reference a ChannelId.");
                }
            }
        }

        return modelState.ErrorCount == 0;
    }

    private async Task PublishAlarmConfigChangedAsync(int teamId)
    {
        var sub = redis.GetSubscriber();
        var channel = RedisChannel.Literal(string.Format(Consts.ALARM_CONFIG_CHANGED_CHANNEL, teamId));
        await sub.PublishAsync(channel, RedisValue.EmptyString);
    }

    private async Task PublishAlarmChangeAsync(AlarmChangeNotification notification)
    {
        var sub = redis.GetSubscriber();
        var channel = RedisChannel.Literal(string.Format(Consts.ALARM_CHANGES_CHANNEL, notification.TeamId));
        await sub.PublishAsync(channel, MessagePackSerializer.Serialize(notification));
    }

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();

    #endregion
}

public record SaveCarConfigurationResult(Guid ConfigurationId, DateTime LastUpdated);

public record TeamRequest(string Name, string ClientId);

public record UserTeam(int Id, string Name, string ClientId, string Roles);

public record CarRequest(string Number, string Make, string Model, string Color);

public record CarUpdate(string Make, string Model, string Color);

