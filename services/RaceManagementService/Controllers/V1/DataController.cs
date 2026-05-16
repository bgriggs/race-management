using System.Text.Json;
using System.Net.Http.Json;
using Channels;
using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceManagementService.Data;
using RaceManagementService.Discovery;
using UnitsNet;

namespace RaceManagementService.Controllers.V1;

[ApiController]
[Route("v{version:apiVersion}/[controller]/[action]")]
[ApiVersion("1.0")]
public class DataController(
    RaceManagementDbContext db,
    ILogger<DataController> logger,
    RacecarRegistry racecarRegistry,
    IHttpClientFactory httpClientFactory) : ControllerBase
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

        UpsertCarConfigurationEntity(carConfiguration, await db.CarConfigurations.FindAsync(carConfiguration.ConfigurationId));

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
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<CarConfiguration>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CarConfiguration>> TransmitToCarAsync([FromBody] CarConfiguration carConfiguration)
    {
        logger.LogInformation("{MethodName} called", nameof(TransmitToCarAsync));

        var activeRacecar = racecarRegistry.ActiveRacecar;
        if (activeRacecar is null)
        {
            logger.LogWarning("Cannot transmit configuration because there is no active racecar.");
            return NotFound("There is no active car.");
        }

        if (carConfiguration.ConfigurationId == Guid.Empty)
            carConfiguration.ConfigurationId = Guid.NewGuid();

        carConfiguration.LastUpdated = DateTime.UtcNow;

        // Save locally first.
        var existing = await db.CarConfigurations.FindAsync(carConfiguration.ConfigurationId);
        UpsertCarConfigurationEntity(carConfiguration, existing);
        await db.SaveChangesAsync();

        var client = httpClientFactory.CreateClient();
        var targetUrl = $"{activeRacecar.BaseUrl}/config";

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(targetUrl, carConfiguration, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to reach active racecar '{RacecarName}' at {TargetUrl}", activeRacecar.Name, targetUrl);
            return StatusCode(StatusCodes.Status502BadGateway, "Failed to send configuration to the active car.");
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Active racecar '{RacecarName}' rejected configuration with status code {StatusCode}.",
                activeRacecar.Name,
                (int)response.StatusCode);
            return StatusCode(StatusCodes.Status502BadGateway, "Active car did not accept configuration.");
        }

        // Mark the successful car transmit timestamp.
        carConfiguration.LastUpdatedOnCarTimestamp = DateTime.UtcNow;
        UpsertCarConfigurationEntity(carConfiguration, existing ?? await db.CarConfigurations.FindAsync(carConfiguration.ConfigurationId));
        await db.SaveChangesAsync();

        // Return the fully update configuration with the new timestamps
        return Ok(carConfiguration);
    }

    private void UpsertCarConfigurationEntity(CarConfiguration carConfiguration, CarConfigurationEntity? existing)
    {
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

            return;
        }

        logger.LogInformation("Updating existing car configuration...");
        existing.Name = carConfiguration.Name;
        existing.Car = carConfiguration.Car;
        existing.Notes = carConfiguration.Notes;
        existing.LastUpdated = carConfiguration.LastUpdated;
        existing.LastUpdatedOnCarTimestamp = carConfiguration.LastUpdatedOnCarTimestamp;
        existing.ConfigurationSchemaVersion = carConfiguration.ConfigurationSchemaVersion;
        existing.Data = JsonSerializer.Serialize(carConfiguration, JsonOptions);
    }

    [HttpGet]
    [Produces("application/json", "application/x-msgpack")]
    [ProducesResponseType<List<ChannelDefinition>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChannelDefinition>>> LoadReservedChannelDefinitionsAsync()
    {
        logger.LogInformation("{MethodName} called", nameof(LoadReservedChannelDefinitionsAsync));
        var channels = new List<ChannelDefinition>
        {
            new() { Id = Guid.Parse("3b63f11f-224b-45d9-8b7a-12be256025a6"), Name = "CoolantTemp", IsReserved = true, Abbreviation = "COOLANT", BaseUnitType = "DegreeFahrenheit", BaseDecimalPlaces = 1 },
            new() { Id = Guid.Parse("c6b14eae-9070-4c2a-91a9-2ce3984d30a7"), Name = "OilTemp", IsReserved = true, Abbreviation = "OILT", BaseUnitType = "DegreeFahrenheit", BaseDecimalPlaces = 1 },
            new() { Id = Guid.Parse("5d7f1ae4-3a6f-45fe-94c2-905e07af4be1"), Name = "OilPress", IsReserved = true, Abbreviation = "OILP", BaseUnitType = "PoundForcePerSquareInch", BaseDecimalPlaces = 1 },
            new() { Id = Guid.Parse("52cba3bc-c5f5-4fb2-8060-c2444eb448c3"), Name = "Battery", IsReserved = true, Abbreviation = "BATT", BaseUnitType = "Volts", BaseDecimalPlaces = 1 },
            new() { Id = Guid.Parse("cf8698bf-3a17-4cb1-b993-c14937ad2fae"), Name = "GPSSpeed", IsReserved = true, Abbreviation = "GSP", BaseUnitType = "MilePerHour", BaseDecimalPlaces = 1 },
            new() { Id = Guid.Parse("a2529acf-a7c6-449f-8a85-c7d76b35dbcb"), Name = "FuelLevel", IsReserved = true, Abbreviation = "FL", BaseUnitType = "UsGallon", BaseDecimalPlaces = 2 },
            new() { Id = Guid.Parse("e5755aa0-3802-4729-9210-3c3e9dc8a644"), Name = "BrakePos", IsReserved = true, Abbreviation = "BP", BaseUnitType = "Percent", BaseDecimalPlaces = 2 },
        };

        return channels;
    }

    [HttpGet]
    [Produces("application/json", "application/x-msgpack")]
    [ProducesResponseType<List<string>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<List<string>> LoadAvailableUnitTypes(string dataType)
    {
        logger.LogInformation("{MethodName} called for DataType={DataType}", nameof(LoadAvailableUnitTypes), dataType);

        if (string.IsNullOrWhiteSpace(dataType))
            return NotFound();

        var quantityInfo = Quantity.Infos.FirstOrDefault(q => q.Name.Equals(dataType, StringComparison.OrdinalIgnoreCase));
        if (quantityInfo is null)
        {
            logger.LogWarning("UnitsNet quantity type not found for '{DataType}'.", dataType);
            return NotFound();
        }

        var unitTypes = quantityInfo.UnitInfos
            .Select(u => u.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();

        return Ok(unitTypes);
    }
}
