namespace CarUpdateAgent.Services;

public interface IUpdateDownloader
{
    /// <summary>
    /// Opens a streaming download from <paramref name="url"/>.
    /// The caller is responsible for disposing the returned stream.
    /// </summary>
    Task<Stream> OpenAsync(string url, CancellationToken cancellationToken);
}
