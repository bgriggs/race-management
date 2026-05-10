using CarUpdateAgent.Configuration;
using CarUpdateAgent.Models;
using CarUpdateAgent.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CarUpdateAgent.Tests;

[TestClass]
public sealed class UpdateOrchestratorTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static IOptions<UpdateAgentOptions> DefaultOptions(string? unit = null) =>
        Options.Create(new UpdateAgentOptions
        {
            CoreAppSystemdUnit = unit ?? "racecar-core.service",
            CoreAppBinaryPath = "/tmp/test/core-app",
            BackupBinaryPath = "/tmp/test/core-app.prev",
            IncomingBinaryPath = "/tmp/test/core-app.incoming",
        });

    private static UpdateOrchestrator BuildOrchestrator(
        Mock<IBinaryStore>? binaryStore = null,
        Mock<IHashVerifier>? hashVerifier = null,
        Mock<ISystemdService>? systemd = null,
        Mock<IUpdateDownloader>? downloader = null,
        Mock<IRollbackWatchdog>? watchdog = null,
        IOptions<UpdateAgentOptions>? options = null)
    {
        binaryStore ??= new Mock<IBinaryStore>();
        hashVerifier ??= new Mock<IHashVerifier>();
        systemd ??= new Mock<ISystemdService>();
        downloader ??= new Mock<IUpdateDownloader>();
        watchdog ??= new Mock<IRollbackWatchdog>();

        binaryStore.SetupGet(b => b.IncomingPath).Returns("/tmp/test/core-app.incoming");

        return new UpdateOrchestrator(
            binaryStore.Object,
            hashVerifier.Object,
            systemd.Object,
            downloader.Object,
            watchdog.Object,
            options ?? DefaultOptions(),
            NullLogger<UpdateOrchestrator>.Instance);
    }

    private static UpdateInfo MakeUpdateInfo(string version = "1.0.0", string hash = "abc123") =>
        new() { Version = version, DownloadUrl = "https://example.com/binary", ExpectedSha256 = hash };

    // -----------------------------------------------------------------------
    // Initial state
    // -----------------------------------------------------------------------

    [TestMethod]
    public void InitialState_IsIdle()
    {
        var sut = BuildOrchestrator();
        Assert.AreEqual(UpdateStatus.Idle, sut.CurrentState.Status);
    }

    // -----------------------------------------------------------------------
    // OTA happy path
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task StartOtaUpdate_HappyPath_TransitionsToSucceeded()
    {
        var binaryStore = new Mock<IBinaryStore>();
        binaryStore.SetupGet(b => b.IncomingPath).Returns("/tmp/test/core-app.incoming");
        binaryStore
            .Setup(b => b.SaveIncomingAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hashVerifier = new Mock<IHashVerifier>();
        hashVerifier
            .Setup(h => h.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var systemd = new Mock<ISystemdService>();

        var downloader = new Mock<IUpdateDownloader>();
        downloader
            .Setup(d => d.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        var watchdog = new Mock<IRollbackWatchdog>();
        watchdog
            .Setup(w => w.WatchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = BuildOrchestrator(binaryStore, hashVerifier, systemd, downloader, watchdog);

        await sut.StartOtaUpdateAsync(MakeUpdateInfo(), CancellationToken.None);

        // Pipeline runs in background — give it time to complete.
        await WaitForStatusAsync(sut, UpdateStatus.Succeeded);

        Assert.AreEqual(UpdateStatus.Succeeded, sut.CurrentState.Status);
        Assert.AreEqual("1.0.0", sut.CurrentState.Version);

        systemd.Verify(s => s.StopAsync("racecar-core.service", It.IsAny<CancellationToken>()), Times.Once);
        systemd.Verify(s => s.StartAsync("racecar-core.service", It.IsAny<CancellationToken>()), Times.Once);
        binaryStore.Verify(b => b.Swap(), Times.Once);
        watchdog.Verify(w => w.WatchAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // -----------------------------------------------------------------------
    // Hash mismatch
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task StartOtaUpdate_HashMismatch_TransitionsToFailed_NoBinarySwap()
    {
        var binaryStore = new Mock<IBinaryStore>();
        binaryStore.SetupGet(b => b.IncomingPath).Returns("/tmp/test/core-app.incoming");
        binaryStore
            .Setup(b => b.SaveIncomingAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hashVerifier = new Mock<IHashVerifier>();
        hashVerifier
            .Setup(h => h.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var systemd = new Mock<ISystemdService>();

        var downloader = new Mock<IUpdateDownloader>();
        downloader
            .Setup(d => d.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        var watchdog = new Mock<IRollbackWatchdog>();

        var sut = BuildOrchestrator(binaryStore, hashVerifier, systemd, downloader, watchdog);

        await sut.StartOtaUpdateAsync(MakeUpdateInfo(), CancellationToken.None);

        await WaitForStatusAsync(sut, UpdateStatus.Failed);

        Assert.AreEqual(UpdateStatus.Failed, sut.CurrentState.Status);
        Assert.IsNotNull(sut.CurrentState.ErrorDetail);

        binaryStore.Verify(b => b.Swap(), Times.Never);
        systemd.Verify(s => s.StopAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        watchdog.Verify(w => w.WatchAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // -----------------------------------------------------------------------
    // Watchdog triggers rollback
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task StartOtaUpdate_WatchdogFails_TransitionsToRolledBack()
    {
        var binaryStore = new Mock<IBinaryStore>();
        binaryStore.SetupGet(b => b.IncomingPath).Returns("/tmp/test/core-app.incoming");
        binaryStore
            .Setup(b => b.SaveIncomingAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hashVerifier = new Mock<IHashVerifier>();
        hashVerifier
            .Setup(h => h.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var systemd = new Mock<ISystemdService>();

        var downloader = new Mock<IUpdateDownloader>();
        downloader
            .Setup(d => d.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        var watchdog = new Mock<IRollbackWatchdog>();
        watchdog
            .Setup(w => w.WatchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // watchdog rolled back

        var sut = BuildOrchestrator(binaryStore, hashVerifier, systemd, downloader, watchdog);

        await sut.StartOtaUpdateAsync(MakeUpdateInfo(), CancellationToken.None);

        await WaitForStatusAsync(sut, UpdateStatus.RolledBack);

        Assert.AreEqual(UpdateStatus.RolledBack, sut.CurrentState.Status);
        Assert.IsNotNull(sut.CurrentState.ErrorDetail);
    }

    // -----------------------------------------------------------------------
    // Concurrent update guard
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task StartOtaUpdate_WhileInProgress_Throws()
    {
        var binaryStore = new Mock<IBinaryStore>();
        binaryStore.SetupGet(b => b.IncomingPath).Returns("/tmp/test/core-app.incoming");
        binaryStore
            .Setup(b => b.SaveIncomingAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hashVerifier = new Mock<IHashVerifier>();
        hashVerifier
            .Setup(h => h.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var downloader = new Mock<IUpdateDownloader>();
        downloader
            .Setup(d => d.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        // Stall the watchdog so the orchestrator stays in WatchingHealth while we try to start a second update.
        var watchdogGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var watchdog = new Mock<IRollbackWatchdog>();
        watchdog
            .Setup(w => w.WatchAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken _) =>
            {
                await watchdogGate.Task;
                return true;
            });

        var systemd = new Mock<ISystemdService>();

        var sut = BuildOrchestrator(binaryStore, hashVerifier, systemd, downloader, watchdog);

        // Start first update and let it reach WatchingHealth.
        await sut.StartOtaUpdateAsync(MakeUpdateInfo("1.0.0"), CancellationToken.None);
        await WaitForStatusAsync(sut, UpdateStatus.WatchingHealth);

        // Second update must be rejected.
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => sut.StartOtaUpdateAsync(MakeUpdateInfo("2.0.0"), CancellationToken.None));

        // Release the watchdog so the test process cleans up.
        watchdogGate.SetResult();
    }

    // -----------------------------------------------------------------------
    // Manual rollback
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task RollbackAsync_CallsRestoreAndRestart()
    {
        var binaryStore = new Mock<IBinaryStore>();
        binaryStore.SetupGet(b => b.IncomingPath).Returns("/tmp/test/core-app.incoming");

        var systemd = new Mock<ISystemdService>();

        var sut = BuildOrchestrator(binaryStore: binaryStore, systemd: systemd);

        await sut.RollbackAsync(CancellationToken.None);

        binaryStore.Verify(b => b.Rollback(), Times.Once);
        systemd.Verify(s => s.RestartAsync("racecar-core.service", It.IsAny<CancellationToken>()), Times.Once);
        Assert.AreEqual(UpdateStatus.RolledBack, sut.CurrentState.Status);
    }

    [TestMethod]
    public async Task RollbackAsync_WhileUpdateInProgress_Throws()
    {
        var binaryStore = new Mock<IBinaryStore>();
        binaryStore.SetupGet(b => b.IncomingPath).Returns("/tmp/test/core-app.incoming");
        binaryStore
            .Setup(b => b.SaveIncomingAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hashVerifier = new Mock<IHashVerifier>();
        hashVerifier
            .Setup(h => h.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var watchdogGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var watchdog = new Mock<IRollbackWatchdog>();
        watchdog
            .Setup(w => w.WatchAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken _) => { await watchdogGate.Task; return true; });

        var systemd = new Mock<ISystemdService>();

        var downloader = new Mock<IUpdateDownloader>();
        downloader
            .Setup(d => d.OpenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        var sut = BuildOrchestrator(binaryStore, hashVerifier, systemd, downloader, watchdog);

        await sut.StartOtaUpdateAsync(MakeUpdateInfo(), CancellationToken.None);
        await WaitForStatusAsync(sut, UpdateStatus.WatchingHealth);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => sut.RollbackAsync(CancellationToken.None));

        watchdogGate.SetResult();
    }

    // -----------------------------------------------------------------------
    // Laptop update path
    // -----------------------------------------------------------------------

    [TestMethod]
    public async Task StartLaptopUpdate_HappyPath_TransitionsToSucceeded()
    {
        var binaryStore = new Mock<IBinaryStore>();
        binaryStore.SetupGet(b => b.IncomingPath).Returns("/tmp/test/core-app.incoming");

        var hashVerifier = new Mock<IHashVerifier>();
        hashVerifier
            .Setup(h => h.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var systemd = new Mock<ISystemdService>();

        var watchdog = new Mock<IRollbackWatchdog>();
        watchdog
            .Setup(w => w.WatchAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = BuildOrchestrator(binaryStore, hashVerifier, systemd, watchdog: watchdog);

        using var binaryStream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await sut.StartLaptopUpdateAsync(binaryStream, "deadbeef", "2.0.0", CancellationToken.None);

        await WaitForStatusAsync(sut, UpdateStatus.Succeeded);

        Assert.AreEqual(UpdateStatus.Succeeded, sut.CurrentState.Status);
        Assert.AreEqual("2.0.0", sut.CurrentState.Version);

        binaryStore.Verify(b => b.SaveIncomingAsync(binaryStream, It.IsAny<CancellationToken>()), Times.Once);
        binaryStore.Verify(b => b.Swap(), Times.Once);
        systemd.Verify(s => s.StopAsync("racecar-core.service", It.IsAny<CancellationToken>()), Times.Once);
        systemd.Verify(s => s.StartAsync("racecar-core.service", It.IsAny<CancellationToken>()), Times.Once);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task WaitForStatusAsync(
        IUpdateOrchestrator orchestrator,
        UpdateStatus expected,
        int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (orchestrator.CurrentState.Status == expected)
                return;
            await Task.Delay(20);
        }

        Assert.Fail(
            $"Timed out waiting for status {expected}. Current status: {orchestrator.CurrentState.Status}");
    }
}
