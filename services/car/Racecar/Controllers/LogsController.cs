using Microsoft.AspNetCore.Mvc;

namespace Racecar.Controllers;

[ApiController]
[Route("logs")]
public class LogsController : ControllerBase
{
    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogger<LogsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns recent structured log entries from the car application log directory.
    /// </summary>
    [HttpGet]
    public IActionResult GetLogs()
    {
        throw new NotImplementedException();
    }
}
