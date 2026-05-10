using Microsoft.AspNetCore.Mvc;

namespace Racecar.Controllers;

[ApiController]
[Route("can")]
public class CanController : ControllerBase
{
    private readonly ILogger<CanController> _logger;

    public CanController(ILogger<CanController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Streams raw CAN frames as a long-poll async sequence for live diagnostic inspection.
    /// Currently returns an empty sequence; CAN bus integration is not yet implemented.
    /// </summary>
    [HttpGet("raw")]
    public IAsyncEnumerable<string> GetRaw(CancellationToken cancellationToken)
    {
        return AsyncEnumerable.Empty<string>();
    }
}
