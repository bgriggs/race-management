using Common.Racecar;
using Microsoft.AspNetCore.Mvc;

namespace Racecar.Controllers;

[ApiController]
[Route("status")]
public class StatusController : ControllerBase
{
    private readonly ILogger<StatusController> _logger;

    public StatusController(ILogger<StatusController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns the current application status including version, config version,
    /// connection states (CAN, cloud), and uptime.
    /// </summary>
    [HttpGet]
    public IActionResult GetStatus()
    {
        throw new NotImplementedException();
    }
}
