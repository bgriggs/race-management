using System.Text.Json;
using Cloud.Shared.Auth;
using Cloud.Shared.Database;
using Cloud.Shared.Database.Models;
using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebApi.Controllers;


[Route("v{version:apiVersion}/[controller]/[action]")]
[ApiVersion("1.0")]
[Produces("application/json")]
public class ConfigurationController(
    ITeamRoleContext teamRoleContext,
    IDbContextFactory<RaceManagementContext> dbFactory) : Controller
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
        var json = JsonSerializer.Serialize(configuration);

        var existing = await db.CarConfigurations.FirstOrDefaultAsync(c => c.Id == id && c.TeamId == teamId, ct);
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
            return race;
        }

        var existing = await db.Races.FirstOrDefaultAsync(r => r.Id == race.Id && r.TeamId == teamId, ct);
        if (existing is null) { return NotFound(); }

        existing.Name = race.Name;
        existing.Start = race.Start;
        existing.Duration = race.Duration;
        existing.Notes = race.Notes;
        existing.RedMistEventId = race.RedMistEventId;
        existing.RedMistOrganizationId = race.RedMistOrganizationId;
        await db.SaveChangesAsync(ct);
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
        var rows = await db.Races
            .Where(r => r.Id == raceId && r.TeamId == teamId)
            .ExecuteDeleteAsync(ct);

        return rows == 0 ? NotFound() : NoContent();
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
            .FirstOrDefaultAsync(c => c.TeamId == teamId && c.UserId == userId, ct);

        if (config is null) { return NotFound(); }
        return config;
    }

    [HttpPost]
    public async Task<IActionResult> SaveChannelStatusTableConfigurationAsync(int teamId, string userId, [FromBody] ChannelStatusTableConfiguration configuration, CancellationToken ct)
    {
        if (!await teamRoleContext.IsUserInTeamAsync(teamId, ct)) { return Forbid(); }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var existing = await db.ChannelStatusTableConfigurations.FirstOrDefaultAsync(c => c.TeamId == teamId && c.UserId == userId, ct);
        if (existing is null)
        {
            configuration.TeamId = teamId;
            configuration.UserId = userId;
            db.ChannelStatusTableConfigurations.Add(configuration);
        }
        else
        {
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
}

public record SaveCarConfigurationResult(Guid ConfigurationId, DateTime LastUpdated);

public record TeamRequest(string Name, string ClientId);

public record UserTeam(int Id, string Name, string ClientId, string Roles);

public record CarRequest(string Number, string Make, string Model, string Color);

public record CarUpdate(string Make, string Model, string Color);

