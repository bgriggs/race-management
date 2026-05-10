namespace CarUpdateAgent.Services;

public interface IHashVerifier
{
    /// <summary>
    /// Streams <paramref name="filePath"/> and computes its SHA-256 digest,
    /// then compares it against <paramref name="expectedSha256Hex"/> (case-insensitive).
    /// </summary>
    /// <returns><c>true</c> if the digest matches; <c>false</c> otherwise.</returns>
    Task<bool> VerifyAsync(string filePath, string expectedSha256Hex, CancellationToken cancellationToken);
}
