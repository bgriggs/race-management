using CarUpdateAgent.Configuration;
using Microsoft.Extensions.Options;

namespace CarUpdateAgent.Services;

public class BinaryStore : IBinaryStore
{
    private readonly UpdateAgentOptions _options;

    public BinaryStore(IOptions<UpdateAgentOptions> options)
    {
        _options = options.Value;
    }

    public string IncomingPath => _options.IncomingBinaryPath;

    public bool HasBackup => File.Exists(_options.BackupBinaryPath);

    public async Task SaveIncomingAsync(Stream source, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_options.IncomingBinaryPath);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        await using var file = new FileStream(
            _options.IncomingBinaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await source.CopyToAsync(file, cancellationToken);
    }

    public void Swap()
    {
        // Ensure the active binary's directory exists (first-run or path change)
        var activeDir = Path.GetDirectoryName(_options.CoreAppBinaryPath);
        if (activeDir is not null)
            Directory.CreateDirectory(activeDir);

        // Backup the current active binary before replacing it
        if (File.Exists(_options.CoreAppBinaryPath))
            File.Copy(_options.CoreAppBinaryPath, _options.BackupBinaryPath, overwrite: true);

        // Atomically replace the active binary with the incoming staging file.
        // File.Move with overwrite is atomic on Linux when src and dst are on the same filesystem.
        File.Move(_options.IncomingBinaryPath, _options.CoreAppBinaryPath, overwrite: true);
    }

    public void Rollback()
    {
        if (!HasBackup)
            throw new InvalidOperationException(
                $"No backup binary found at '{_options.BackupBinaryPath}'. Rollback is not possible.");

        File.Copy(_options.BackupBinaryPath, _options.CoreAppBinaryPath, overwrite: true);
    }
}
