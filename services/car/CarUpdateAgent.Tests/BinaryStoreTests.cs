using CarUpdateAgent.Configuration;
using CarUpdateAgent.Services;
using Microsoft.Extensions.Options;

namespace CarUpdateAgent.Tests;

[TestClass]
public sealed class BinaryStoreTests
{
    private string _dir = string.Empty;
    private UpdateAgentOptions _opts = null!;
    private BinaryStore _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_dir);

        _opts = new UpdateAgentOptions
        {
            CoreAppBinaryPath = Path.Combine(_dir, "core-app"),
            BackupBinaryPath = Path.Combine(_dir, "core-app.prev"),
            IncomingBinaryPath = Path.Combine(_dir, "core-app.incoming"),
        };

        _sut = new BinaryStore(Options.Create(_opts));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    // -----------------------------------------------------------------------
    // SaveIncomingAsync
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task SaveIncomingAsync_WritesStreamToDisk()
    {
        var data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        await _sut.SaveIncomingAsync(new MemoryStream(data), CancellationToken.None);

        Assert.IsTrue(File.Exists(_opts.IncomingBinaryPath));
        CollectionAssert.AreEqual(data, await File.ReadAllBytesAsync(_opts.IncomingBinaryPath));
    }

    // -----------------------------------------------------------------------
    // HasBackup
    // -----------------------------------------------------------------------

    [TestMethod]
    public void HasBackup_NoBackupFile_ReturnsFalse()
    {
        Assert.IsFalse(_sut.HasBackup);
    }

    [TestMethod]
    public async Task HasBackup_AfterSwap_ReturnsTrue()
    {
        await WriteFileAsync(_opts.CoreAppBinaryPath, [0x01]);
        await WriteFileAsync(_opts.IncomingBinaryPath, [0x02]);

        _sut.Swap();

        Assert.IsTrue(_sut.HasBackup);
    }

    // -----------------------------------------------------------------------
    // Swap
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task Swap_PromotesIncomingToActive()
    {
        var incoming = new byte[] { 0xCA, 0xFE };
        await WriteFileAsync(_opts.IncomingBinaryPath, incoming);

        _sut.Swap();

        CollectionAssert.AreEqual(incoming, await File.ReadAllBytesAsync(_opts.CoreAppBinaryPath));
    }

    [TestMethod]
    public async Task Swap_BacksUpPreviousActiveBeforeReplacing()
    {
        var original = new byte[] { 0x01 };
        var incoming = new byte[] { 0x02 };

        await WriteFileAsync(_opts.CoreAppBinaryPath, original);
        await WriteFileAsync(_opts.IncomingBinaryPath, incoming);

        _sut.Swap();

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(_opts.BackupBinaryPath));
        CollectionAssert.AreEqual(incoming, await File.ReadAllBytesAsync(_opts.CoreAppBinaryPath));
    }

    [TestMethod]
    public async Task Swap_IncomingNoLongerExistsAfterSwap()
    {
        await WriteFileAsync(_opts.IncomingBinaryPath, [0xFF]);

        _sut.Swap();

        Assert.IsFalse(File.Exists(_opts.IncomingBinaryPath));
    }

    // -----------------------------------------------------------------------
    // Rollback
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task Rollback_RestoresBackupToActive()
    {
        var original = new byte[] { 0xAA };
        var updated = new byte[] { 0xBB };

        await WriteFileAsync(_opts.CoreAppBinaryPath, original);
        await WriteFileAsync(_opts.IncomingBinaryPath, updated);
        _sut.Swap();

        _sut.Rollback();

        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(_opts.CoreAppBinaryPath));
    }

    [TestMethod]
    public void Rollback_NoBackup_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => _sut.Rollback());
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task WriteFileAsync(string path, byte[] content)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir is not null)
            Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(path, content);
    }
}
