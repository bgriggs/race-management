using System.Security.Cryptography;

namespace CarUpdateAgent.Services;

public class HashVerifier : IHashVerifier
{
    public async Task<bool> VerifyAsync(
        string filePath,
        string expectedSha256Hex,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();

        return string.Equals(actual, expectedSha256Hex.ToLowerInvariant(), StringComparison.Ordinal);
    }
}
