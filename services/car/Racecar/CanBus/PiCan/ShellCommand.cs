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
    /// Run command and don't wait on the process.
    /// </summary>
    /// <param name="cmd"></param>
    /// <param name="args"></param>
    public async Task RunInstAsync(string cmd, string args = "")
    {
        var process = new Process
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
        //Logger.Trace(process.StartInfo.FileName + " " + process.StartInfo.Arguments);
        await Task.Run(() =>
        {
            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error sending CAN message");
            }
        });
        //await new Task(() => { process.Start(); });
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

