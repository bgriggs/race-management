using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Threading.Channels;

namespace Racecar.Controllers;

[ApiController]
[Route("logs")]
public class LogsController : ControllerBase
{
    private const string LogFileName = "racecar.log";

    private readonly ILogger<LogsController> _logger;

    public LogsController(ILogger<LogsController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Streams log entries to the client via Server-Sent Events. All existing log content
    /// is sent first, followed by new entries as they are written.
    /// </summary>
    [HttpGet("stream")]
    public async Task Stream(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var logPath = Path.Combine(AppContext.BaseDirectory, LogFileName);

        if (!System.IO.File.Exists(logPath))
        {
            await Response.WriteAsync("data: [Log file not found]\n\n", cancellationToken);
            return;
        }

        var logDir = Path.GetDirectoryName(logPath)!;

        // Set up the watcher before reading so no writes are missed.
        var channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleWriter = false,
            SingleReader = true
        });

        using var watcher = new FileSystemWatcher(logDir, LogFileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        watcher.Changed += (_, _) => channel.Writer.TryWrite(true);

        using var fileStream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fileStream, Encoding.UTF8);

        // Send all existing content.
        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            await WriteSseLineAsync(line, cancellationToken);
        }

        await Response.Body.FlushAsync(cancellationToken);
        long lastPosition = fileStream.Position;

        // Stream new lines as they are written.
        try
        {
            await foreach (var _ in channel.Reader.ReadAllAsync(cancellationToken))
            {
                fileStream.Seek(lastPosition, SeekOrigin.Begin);

                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    await WriteSseLineAsync(line, cancellationToken);
                    lastPosition = fileStream.Position;
                }

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
        // SSE data fields cannot contain raw newlines; escape any embedded ones.
        var escaped = line.Replace("\n", "\ndata: ");
        return Response.WriteAsync($"data: {escaped}\n\n", cancellationToken);
    }
}
