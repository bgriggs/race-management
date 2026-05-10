using System.Diagnostics;

namespace Racecar.CanBus.PiCan;

/// <summary>
/// Works with Bash shell to command the CAN utility.
/// </summary>
public class ShellCommand : IDisposable
{
    private ILogger Logger { get; }
    private Process? process;
    public event Action<string>? ReceivedOutput;
    

    public ShellCommand(ILoggerFactory loggerFactory)
    {
        Logger = loggerFactory.CreateLogger(GetType().Name);
    }


    public int Run(string cmd, string args = "")
    {
        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        Logger.LogInformation(process.StartInfo.FileName + " " + process.StartInfo.Arguments);
        process.Start();

        while (!process.HasExited)
        {
            var resp = process.StandardOutput.ReadLine();
            if (resp != null)
            {
                ReceivedOutput?.Invoke(resp);
            }
        }

        return process.ExitCode;
    }

    /// <summary>
    /// Run command, wait for it to complete, and return its exit code.
    /// </summary>
    public async Task<int> RunInstAsync(string cmd, string args = "")
    {
        var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = cmd,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        Logger.LogInformation("{Cmd} {Args}", cmd, args);

        proc.Start();
        string stdout = await proc.StandardOutput.ReadToEndAsync();
        string stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (!string.IsNullOrWhiteSpace(stdout))
            Logger.LogDebug("stdout: {Output}", stdout.Trim());
        if (!string.IsNullOrWhiteSpace(stderr))
            Logger.LogWarning("stderr: {Error}", stderr.Trim());

        return proc.ExitCode;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        try
        {
            if (process != null)
            {
                process.Kill();
                process.Dispose();
                process = null;
            }
        }
        catch { }
    }
}

