using CarUpdateAgent.Services;

namespace CarUpdateAgent.Tests;

[TestClass]
public sealed class HashVerifierTests
{
    private readonly HashVerifier _sut = new();

    [TestMethod]
    public async Task VerifyAsync_CorrectHash_ReturnsTrue()
    {
        var (path, expected) = await CreateTempFileWithHashAsync([0x01, 0x02, 0x03, 0x04]);
        Assert.IsTrue(await _sut.VerifyAsync(path, expected, CancellationToken.None));
    }

    [TestMethod]
    public async Task VerifyAsync_WrongHash_ReturnsFalse()
    {
        var (path, _) = await CreateTempFileWithHashAsync([0x01, 0x02, 0x03, 0x04]);
        Assert.IsFalse(await _sut.VerifyAsync(path, "0000000000000000000000000000000000000000000000000000000000000000", CancellationToken.None));
    }

    [TestMethod]
    public async Task VerifyAsync_HashIsCaseInsensitive()
    {
        var (path, expectedLower) = await CreateTempFileWithHashAsync([0xAB, 0xCD, 0xEF]);
        var expectedUpper = expectedLower.ToUpperInvariant();
        Assert.IsTrue(await _sut.VerifyAsync(path, expectedUpper, CancellationToken.None));
    }

    [TestMethod]
    public async Task VerifyAsync_EmptyFile_MatchesKnownHash()
    {
        // SHA-256 of empty input is well-known.
        const string emptyHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, []);
            Assert.IsTrue(await _sut.VerifyAsync(path, emptyHash, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task<(string Path, string Sha256Hex)> CreateTempFileWithHashAsync(byte[] content)
    {
        var path = Path.GetTempFileName();
        await File.WriteAllBytesAsync(path, content);

        using var stream = File.OpenRead(path);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();

        return (path, hex);
    }
}
