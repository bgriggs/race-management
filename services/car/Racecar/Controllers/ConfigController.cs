using Common;
using Common.Racecar;
using Microsoft.AspNetCore.Mvc;

namespace Racecar.Controllers;

[ApiController]
[Route("config")]
public class ConfigController : ControllerBase
{
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(ILogger<ConfigController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns the current active car configuration.
    /// </summary>
    [HttpGet]
    public IActionResult GetConfig()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Receives a new configuration from the pit laptop.
    /// The configuration is written atomically and takes effect on next restart.
    /// </summary>
    [HttpPost]
    public IActionResult PostConfig([FromBody] CarConfiguration configuration)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Lists stored configuration versions available for rollback.
    /// </summary>
    [HttpGet("versions")]
    public IActionResult GetVersions()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Rolls back to a previously stored configuration version.
    /// </summary>
    [HttpPost("rollback/{version}")]
    public IActionResult PostRollback(string version)
    {
        throw new NotImplementedException();
    }
}
