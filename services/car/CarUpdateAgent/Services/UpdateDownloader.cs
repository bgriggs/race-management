namespace CarUpdateAgent.Services;

public class UpdateDownloader : IUpdateDownloader
{
    private readonly HttpClient _httpClient;

    public UpdateDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Stream> OpenAsync(string url, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }
}
