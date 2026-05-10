using Microsoft.AspNetCore.Mvc;
using NLog;
using NLog.Config;
using Racecar.Services;

namespace Racecar.Controllers;

[ApiController]
[Route("logs")]
public class LogsController : ControllerBase
{
    private readonly ILogger<LogsController> _logger;
    private readonly LogBroadcaster _broadcaster;

    public LogsController(ILogger<LogsController> logger, LogBroadcaster broadcaster)
    {
        _logger = logger;
        _broadcaster = broadcaster;
    }

    /// <summary>
    /// Streams log entries to the client via Server-Sent Events. Recent log history is
    /// sent immediately, followed by new entries in real time via an in-memory channel.
    /// </summary>
    [HttpGet("stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        using var subscription = _broadcaster.Subscribe();

        // Send buffered history to the newly connected client.
        foreach (var entry in subscription.History)
            await WriteSseLineAsync(entry, cancellationToken);

        await Response.Body.FlushAsync(cancellationToken);

        // Stream new entries as they arrive.
        try
        {
            await foreach (var entry in subscription.Reader.ReadAllAsync(cancellationToken))
            {
                await WriteSseLineAsync(entry, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — exit cleanly.
        }
    }

    private Task WriteSseLineAsync(string line, CancellationToken cancellationToken)
    {
        var escaped = line.Replace("\n", "\ndata: ");
        return Response.WriteAsync($"data: {escaped}\n\n", cancellationToken);
    }

    /// <summary>
    /// Returns the current effective minimum log level for application loggers.
    /// </summary>
    [HttpGet("level")]
    public IActionResult GetLevel()
    {
        var level = GetAppLogLevel();
        return Ok(new { level = level?.Name ?? "Unknown" });
    }

    /// <summary>
    /// Overrides the minimum log level for application loggers at runtime.
    /// Accepted values: Trace, Debug, Info, Warn, Error, Fatal, Off
    /// </summary>
    [HttpPut("level")]
    public IActionResult SetLevel([FromBody] SetLogLevelRequest request)
    {
        var level = NLog.LogLevel.FromString(request.Level);

        var config = LogManager.Configuration;
        if (config is null)
            return StatusCode(503, "NLog configuration is not available.");

        bool changed = false;
        foreach (LoggingRule rule in config.LoggingRules)
        {
            // Only adjust the catch-all application rule, not the System.*/Microsoft.* suppressors.
            if (rule.LoggerNamePattern == "*")
            {
                rule.SetLoggingLevels(level, NLog.LogLevel.Fatal);
                changed = true;
            }
        }

        if (!changed)
            return NotFound("No matching logging rule found.");

        LogManager.ReconfigExistingLoggers();
        _logger.LogInformation("Log level changed to {Level}", level.Name);
        return Ok(new { level = level.Name });
    }

    private static NLog.LogLevel? GetAppLogLevel()
    {
        var config = LogManager.Configuration;
        if (config is null)
            return null;

        foreach (LoggingRule rule in config.LoggingRules)
        {
            if (rule.LoggerNamePattern == "*")
            {
                for (int i = 0; i <= NLog.LogLevel.Fatal.Ordinal; i++)
                {
                    var candidate = NLog.LogLevel.FromOrdinal(i);
                    if (rule.IsLoggingEnabledForLevel(candidate))
                        return candidate;
                }
            }
        }

        return null;
    }
}

public record SetLogLevelRequest(string Level);
