using Microsoft.AspNetCore.Mvc;
using RaceManagementService.Discovery;

namespace RaceManagementService.Controllers.V1;

[ApiController]
[Route("v{version:apiVersion}/[controller]/[action]")]
[ApiVersion("1.0")]
public class DiscoveryController(RacecarRegistry registry, ILogger<DiscoveryController> logger) : ControllerBase
{
    /// <summary>
    /// Returns the list of racecars currently visible on the local network via DNS-SD.
    /// The list is refreshed every ~5 seconds in the background.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<List<DiscoveredRacecar>>(StatusCodes.Status200OK)]
    public ActionResult<List<DiscoveredRacecar>> ListRacecars()
    {
        logger.LogInformation("{MethodName} called", nameof(ListRacecars));
        return Ok(registry.Racecars.ToList());
    }

    /// <summary>
    /// Returns the currently selected active racecar, or 204 No Content if none is selected.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<DiscoveredRacecar>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult<DiscoveredRacecar> GetActiveRacecar()
    {
        logger.LogInformation("{MethodName} called", nameof(GetActiveRacecar));
        var active = registry.ActiveRacecar;
        if (active is null)
            return NoContent();
        return Ok(active);
    }

    /// <summary>
    /// Selects the active racecar by name. The named car must be in the current discovered list.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<DiscoveredRacecar>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<DiscoveredRacecar> SelectRacecar([FromQuery] string name)
    {
        logger.LogInformation("{MethodName} called for '{Name}'", nameof(SelectRacecar), name);

        if (!registry.TrySetActive(name))
        {
            logger.LogWarning("Racecar '{Name}' not found in the current discovered list.", name);
            return NotFound($"Racecar '{name}' is not currently visible on the network.");
        }

        logger.LogInformation("Active racecar set to '{Name}'.", name);
        return Ok(registry.ActiveRacecar);
    }
}
