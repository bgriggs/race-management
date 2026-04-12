using Common;
using Microsoft.AspNetCore.Mvc;

namespace RaceManagementService.Controllers;

[ApiController]
[Route("v{version:apiVersion}/[controller]/[action]")]
[ApiVersion("1.0")]
public class DataController : ControllerBase
{
   

    [HttpGet(Name = "car-configuration")]
    [Produces("application/json", "application/x-msgpack")]
    [ProducesResponseType<CarConfiguration>(StatusCodes.Status200OK)]
    public Task<ActionResult<CarConfiguration>> LoadCarConfigurationAsync(Guid configId)
    {
        return Task.FromResult<ActionResult<CarConfiguration>>(Ok(new CarConfiguration()));
    }

    [HttpGet(Name = "save-car-configuration")]
    [Produces("application/json", "application/x-msgpack")]
    [ProducesResponseType<CarConfiguration>(StatusCodes.Status200OK)]
    public Task<ActionResult<CarConfiguration>> SaveCarConfigurationAsync(CarConfiguration carConfiguration)
    {
        return Task.FromResult<ActionResult<CarConfiguration>>(Ok(carConfiguration));
    }
}
