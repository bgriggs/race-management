using System.Text.Json;
using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceManagementService.Data;

namespace RaceManagementService.Controllers.V1;

[ApiController]
[Route("v{version:apiVersion}/[controller]/[action]")]
[ApiVersion("1.0")]
public class DataController(RaceManagementDbContext db, ILogger<DataController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    [Produces("application/json", "application/x-msgpack")]
    [ProducesResponseType<List<CarConfigurationSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CarConfigurationSummary>>> LoadCarConfigurationSummariesAsync()
    {
        logger.LogInformation("{MethodName} called", nameof(LoadCarConfigurationSummariesAsync));

        var summaries = await db.CarConfigurations
            .Select(e => new CarConfigurationSummary
            {
                Id = e.Id,
                Name = e.Name,
                Car = e.Car,
                Notes = e.Notes,
                LastUpdated = e.LastUpdated,
                ConfigurationSchemaVersion = e.ConfigurationSchemaVersion,
                LastUpdatedOnCarTimestamp = e.LastUpdatedOnCarTimestamp,
            })
            .ToListAsync();

        return Ok(summaries);
    }

    [HttpGet]
    [Produces("application/json", "application/x-msgpack")]
    [ProducesResponseType<CarConfiguration>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CarConfiguration>> LoadCarConfigurationAsync(Guid configId)
    {
        logger.LogInformation("{MethodName} called", nameof(LoadCarConfigurationAsync));

        var entity = await db.CarConfigurations.FindAsync(configId);
        if (entity is null)
        {
            logger.LogWarning("Cannot load configuration specified, not found.");
            return NotFound();
        }

        var config = JsonSerializer.Deserialize<CarConfiguration>(entity.Data, JsonOptions);
        if (config is null)
        {
            logger.LogWarning("Cannot deserialize configuration specified, invalid data.");
            return NotFound();
        }

        config.Car = entity.Car;
        config.LastUpdatedOnCarTimestamp = entity.LastUpdatedOnCarTimestamp;

        return Ok(config);
    }

    [HttpPost]
    [Produces("application/json", "application/x-msgpack")]
    [ProducesResponseType<CarConfiguration>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CarConfiguration>> SaveCarConfigurationAsync([FromBody] CarConfiguration carConfiguration)
    {
        logger.LogInformation("{MethodName} called", nameof(SaveCarConfigurationAsync));

        if (carConfiguration.ConfigurationId == Guid.Empty)
            carConfiguration.ConfigurationId = Guid.NewGuid();

        carConfiguration.LastUpdated = DateTime.UtcNow;

        var existing = await db.CarConfigurations.FindAsync(carConfiguration.ConfigurationId);
        if (existing is null)
        {
            logger.LogInformation("Saving new car configuration...");
            db.CarConfigurations.Add(new CarConfigurationEntity
            {
                Id = carConfiguration.ConfigurationId,
                Name = carConfiguration.Name,
                Car = carConfiguration.Car,
                Notes = carConfiguration.Notes,
                LastUpdated = carConfiguration.LastUpdated,
                LastUpdatedOnCarTimestamp = carConfiguration.LastUpdatedOnCarTimestamp,
                ConfigurationSchemaVersion = carConfiguration.ConfigurationSchemaVersion,
                Data = JsonSerializer.Serialize(carConfiguration, JsonOptions),
            });
        }
        else
        {
            logger.LogInformation("Updating existing car configuration...");
            existing.Name = carConfiguration.Name;
            existing.Car = carConfiguration.Car;
            existing.Notes = carConfiguration.Notes;
            existing.LastUpdated = carConfiguration.LastUpdated;
            existing.LastUpdatedOnCarTimestamp = carConfiguration.LastUpdatedOnCarTimestamp;
            existing.ConfigurationSchemaVersion = carConfiguration.ConfigurationSchemaVersion;
            existing.Data = JsonSerializer.Serialize(carConfiguration, JsonOptions);
        }

        await db.SaveChangesAsync();
        return Ok(carConfiguration);
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCarConfigurationAsync(Guid id)
    {
        logger.LogInformation("{MethodName} called", nameof(DeleteCarConfigurationAsync));

        var entity = await db.CarConfigurations.FindAsync(id);
        if (entity is null)
        {
            logger.LogWarning("Cannot delete configuration specified, not found.");
            return NotFound();
        }

        db.CarConfigurations.Remove(entity);
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<CarConfiguration>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CarConfiguration>> TransmitToCarAsync([FromBody] CarConfiguration carConfiguration)
    {
        logger.LogInformation("{MethodName} called", nameof(TransmitToCarAsync));

        // Save the configuration
        // Send to car
        // Update the last updated on car timestamp

        // Return the fully update configuration with the new timestamps
        return Ok(carConfiguration);
    }
}
