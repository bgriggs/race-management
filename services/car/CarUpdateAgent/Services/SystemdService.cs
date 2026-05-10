using System.Diagnostics;

namespace CarUpdateAgent.Services;

public class SystemdService : ISystemdService
{
    public Task StopAsync(string unit, CancellationToken cancellationToken) =>
        RunAsync("stop", unit, cancellationToken);

    public Task StartAsync(string unit, CancellationToken cancellationToken) =>
        RunAsync("start", unit, cancellationToken);

    public Task RestartAsync(string unit, CancellationToken cancellationToken) =>
        RunAsync("restart", unit, cancellationToken);

    public async Task<bool> IsActiveAsync(string unit, CancellationToken cancellationToken)
    {
        var (_, stdout, _) = await RunAsync("is-active", unit, cancellationToken, throwOnFailure: false);
        return stdout.Trim() == "active";
    }

    public async Task<bool> IsFailedAsync(string unit, CancellationToken cancellationToken)
    {
        var (_, stdout, _) = await RunAsync("is-failed", unit, cancellationToken, throwOnFailure: false);
        return stdout.Trim() == "failed";
    }

    private static async Task RunAsync(string command, string unit, CancellationToken cancellationToken) =>
        await RunAsync(command, unit, cancellationToken, throwOnFailure: true);

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string command,
        string unit,
        CancellationToken cancellationToken,
        bool throwOnFailure)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "systemctl",
                ArgumentList = { command, unit },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (throwOnFailure && process.ExitCode != 0)
            throw new InvalidOperationException(
                $"systemctl {command} {unit} exited with code {process.ExitCode}. Stderr: {stderr.Trim()}");

        return (process.ExitCode, stdout, stderr);
    }
}
