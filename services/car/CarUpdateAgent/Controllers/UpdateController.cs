using CarUpdateAgent.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarUpdateAgent.Controllers;

[ApiController]
[Route("")]
public class UpdateController : ControllerBase
{
    private readonly IUpdateOrchestrator _orchestrator;
    private readonly ILogger<UpdateController> _logger;

    public UpdateController(IUpdateOrchestrator orchestrator, ILogger<UpdateController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Returns the current update lifecycle state.
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(_orchestrator.CurrentState);

    /// <summary>
    /// Accepts a binary stream from the pit laptop and initiates an update.
    /// The binary is read from the request body. The expected SHA-256 hex digest must
    /// be supplied in the <c>X-Expected-Hash</c> header and the version string in
    /// <c>X-Version</c>.
    /// Returns 202 Accepted immediately; poll GET /status for progress.
    /// </summary>
    [HttpPost("update")]
    [RequestSizeLimit(500 * 1024 * 1024)] // 500 MB safety cap
    public async Task<IActionResult> PostUpdate(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Expected-Hash", out var hashValues)
            || string.IsNullOrWhiteSpace(hashValues))
        {
            return BadRequest("Missing required header: X-Expected-Hash");
        }

        if (!Request.Headers.TryGetValue("X-Version", out var versionValues)
            || string.IsNullOrWhiteSpace(versionValues))
        {
            return BadRequest("Missing required header: X-Version");
        }

        var expectedHash = hashValues.ToString();
        var version = versionValues.ToString();

        _logger.LogInformation(
            "Laptop update request received for version {Version} (hash prefix {HashPrefix}…).",
            version,
            expectedHash[..Math.Min(8, expectedHash.Length)]);

        try
        {
            // SaveIncomingAsync drains the request body stream before returning.
            await _orchestrator.StartLaptopUpdateAsync(Request.Body, expectedHash, version, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }

        return Accepted();
    }

    /// <summary>
    /// Triggers an explicit rollback to the previous binary.
    /// Returns 409 Conflict if an update is currently in progress.
    /// </summary>
    [HttpPost("rollback")]
    public async Task<IActionResult> PostRollback(CancellationToken cancellationToken)
    {
        try
        {
            await _orchestrator.RollbackAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }

        return Ok(_orchestrator.CurrentState);
    }
}
