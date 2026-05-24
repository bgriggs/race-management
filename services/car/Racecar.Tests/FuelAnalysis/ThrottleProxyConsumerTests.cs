using Channels;
using Common;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Racecar.FuelAnalysis;
using Racecar.Pipeline;

namespace Racecar.Tests.FuelAnalysis;

[TestClass]
public sealed class ThrottleProxyConsumerTests
{
    public TestContext TestContext { get; set; } = null!;

    private sealed class CapturingInjector : IDerivedChannelInjector
    {
        public List<InternalChannelValue> Emissions { get; } = [];
        public void InjectDerived(ReadOnlySpan<InternalChannelValue> values)
        {
            foreach (var v in values) Emissions.Add(v);
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private static long S(double seconds) => (long)(seconds * TimeSpan.TicksPerSecond);

    private const int TpsId = 0;
    private const int RpmId = 1;
    private const int TripFuelId = 2;
    private const int FuelUsedId = 3;
    private const int FuelFullId = 4;
    private const int InPitId = 5;
    private const int OutFuelUsedId = 10;
    private const int OutRateId = 11;
    private const int OutConfidenceId = 12;
    private const int OutGridCoverageId = 13;

    private static ActiveConfiguration BuildConfig()
    {
        ChannelDefinition Def(Guid id) => new() { Id = id };

        var tps = ReservedChannels.Channels.First(c => c.Name == "ThrottlePosition").Id;
        var rpm = ReservedChannels.Channels.First(c => c.Name == "EngineRPM").Id;
        var trip = ReservedChannels.Channels.First(c => c.Name == "TripFuel").Id;
        var used = ReservedChannels.Channels.First(c => c.Name == "FuelUsed").Id;
        var full = ReservedChannels.Channels.First(c => c.Name == "FuelFull").Id;
        var pit = ReservedChannels.Channels.First(c => c.Name == "InPit").Id;
        var outFuel = ReservedChannels.Channels.First(c => c.Name == "ThrottleProxyFuelUsed").Id;
        var outRate = ReservedChannels.Channels.First(c => c.Name == "ThrottleProxyRate").Id;
        var outConf = ReservedChannels.Channels.First(c => c.Name == "ThrottleProxyConfidence").Id;
        var outCov = ReservedChannels.Channels.First(c => c.Name == "ThrottleProxyGridCoverage").Id;

        var channels = new Dictionary<int, ChannelDefinition>
        {
            [TpsId] = Def(tps),
            [RpmId] = Def(rpm),
            [TripFuelId] = Def(trip),
            [FuelUsedId] = Def(used),
            [FuelFullId] = Def(full),
            [InPitId] = Def(pit),
            [OutFuelUsedId] = Def(outFuel),
            [OutRateId] = Def(outRate),
            [OutConfidenceId] = Def(outConf),
            [OutGridCoverageId] = Def(outCov),
        };
        var guidToId = channels.ToDictionary(kv => kv.Value.Id, kv => kv.Key);
        return ActiveConfiguration.Empty with
        {
            Channels = channels,
            ChannelIdsByDefinition = guidToId,
        };
    }

    private static CarConfiguration BuildCarConfig(int rpmMax = 8000)
    {
        var cfg = new CarConfiguration
        {
            Car = "42",
            ClientId = "racecar",
            ClientSecret = "secret",
        };
        cfg.FuelConfig.ThrottleConsumption.MaxRpm = rpmMax;
        return cfg;
    }

    private static string NewTempPath() =>
        Path.Combine(Path.GetTempPath(), "racecar-tp-" + Guid.NewGuid().ToString("N"), "fuel-calibration.json");

    private static FuelAnalysisOptions FastOptions(string path) => new()
    {
        CalibrationFilePath = path,
        MinCellSamples = 3,
        KConvergenceSampleCount = 3,
        EngineOffResetAfter = TimeSpan.FromSeconds(30),
        EcuResetWindow = TimeSpan.FromSeconds(60),
        EcuResetTripFuelThresholdGallons = 1.0,
        EmitInterval = TimeSpan.FromSeconds(1),
        CoverageEmitInterval = TimeSpan.FromSeconds(30),
        RateEmaTimeConstant = TimeSpan.FromSeconds(2),
        LearningEmaAlpha = 0.3,
    };

    private static ThrottleProxyConsumer BuildConsumer(
        FakeTimeProvider time,
        ActiveConfiguration config,
        CarConfiguration carConfig,
        FuelAnalysisOptions options,
        out CapturingInjector injector,
        out ThrottleProxyCalibrationStore store)
    {
        injector = new CapturingInjector();
        store = new ThrottleProxyCalibrationStore(options.CalibrationFilePath);
        var consumer = new ThrottleProxyConsumer(
            injector,
            () => config,
            new StaticOptionsMonitor<CarConfiguration>(carConfig),
            Options.Create(options),
            store,
            time,
            NullLogger<ThrottleProxyConsumer>.Instance);
        return consumer;
    }

    [TestMethod]
    public void Get_subscriptions_returns_six_input_channel_ids()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 24, 0, 0, 0, TimeSpan.Zero));
        var config = BuildConfig();
        var consumer = BuildConsumer(time, config, BuildCarConfig(), FastOptions(NewTempPath()), out _, out _);

        var subs = consumer.GetSubscriptions(config);
        Assert.IsNotNull(subs);
        Assert.HasCount(6, subs);
        Assert.Contains(TpsId, subs);
        Assert.Contains(RpmId, subs);
        Assert.Contains(TripFuelId, subs);
        Assert.Contains(FuelUsedId, subs);
        Assert.Contains(FuelFullId, subs);
        Assert.Contains(InPitId, subs);
    }

    [TestMethod]
    public async Task Without_calibration_emits_only_confidence_not_fuel_used_or_rate()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 24, 0, 0, 0, TimeSpan.Zero));
        var config = BuildConfig();
        var consumer = BuildConsumer(time, config, BuildCarConfig(), FastOptions(NewTempPath()), out var injector, out _);

        _ = consumer.GetSubscriptions(config);
        await consumer.StartAsync(default);
        await Task.Delay(50, TestContext.CancellationToken);

        // Feed a few TPS/RPM samples — but no calibration window will close, so k stays NaN.
        for (var i = 0; i < 10; i++)
        {
            await consumer.HandleAsync(new[]
            {
                new InternalChannelValue(TpsId, 50, S(i * 0.1), time.GetUtcNow().UtcDateTime),
                new InternalChannelValue(RpmId, 4000, S(i * 0.1), time.GetUtcNow().UtcDateTime),
            }, default);
        }

        time.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(150, TestContext.CancellationToken);

        Assert.IsFalse(injector.Emissions.Any(e => e.ChannelId == OutFuelUsedId),
            "FuelUsed must not be emitted before k is learned.");
        Assert.IsFalse(injector.Emissions.Any(e => e.ChannelId == OutRateId),
            "Rate must not be emitted before any grid cell is calibrated.");
        Assert.IsTrue(injector.Emissions.Any(e => e.ChannelId == OutConfidenceId),
            "Confidence should still emit (value 0).");

        await consumer.DisposeAsync();
    }

    [TestMethod]
    public async Task Full_calibration_loop_learns_k_and_emits_fuel_used()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 24, 0, 0, 0, TimeSpan.Zero));
        var config = BuildConfig();
        var path = NewTempPath();
        var consumer = BuildConsumer(time, config, BuildCarConfig(), FastOptions(path), out var injector, out var store);

        _ = consumer.GetSubscriptions(config);
        await consumer.StartAsync(default);
        await Task.Delay(50, TestContext.CancellationToken);

        // 1. Open: seed TripFuel high, drive a 0.1s TPS sample at 50%/4000 to bootstrap totalizer.
        await consumer.HandleAsync(new[]
        {
            new InternalChannelValue(TripFuelId, 5.0, S(0), time.GetUtcNow().UtcDateTime),
        }, default);

        // 2. FuelFull asserts, then TripFuel drops below 1.0 within window → Reset-Confirmed → window opens.
        await consumer.HandleAsync(new[]
        {
            new InternalChannelValue(FuelFullId, 1.0, S(1), time.GetUtcNow().UtcDateTime),
        }, default);
        await consumer.HandleAsync(new[]
        {
            new InternalChannelValue(TripFuelId, 0.1, S(2), time.GetUtcNow().UtcDateTime),
        }, default);

        // 3. Drive for 60 seconds at 50% TPS, 4000 RPM.
        for (var i = 0; i < 600; i++)
        {
            var ticks = S(3.0 + i * 0.1);
            await consumer.HandleAsync(new[]
            {
                new InternalChannelValue(TpsId, 50, ticks, time.GetUtcNow().UtcDateTime),
                new InternalChannelValue(RpmId, 4000, ticks, time.GetUtcNow().UtcDateTime),
            }, default);
        }
        // 4. Arrive at pit and assert FuelFull with TripFuel = 1.5 gal (1.5 - 0.1 = 1.4 gal used in window).
        await consumer.HandleAsync(new[]
        {
            new InternalChannelValue(InPitId, 1.0, S(70), time.GetUtcNow().UtcDateTime),
        }, default);
        await consumer.HandleAsync(new[]
        {
            new InternalChannelValue(TripFuelId, 1.5, S(70), time.GetUtcNow().UtcDateTime),
            new InternalChannelValue(FuelFullId, 1.0, S(70), time.GetUtcNow().UtcDateTime),
        }, default);

        // 5. Advance the wall clock; trigger emit tick.
        time.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(200, TestContext.CancellationToken);

        var fuelUsedEmissions = injector.Emissions.Where(e => e.ChannelId == OutFuelUsedId).ToList();
        Assert.IsGreaterThanOrEqualTo(1, fuelUsedEmissions.Count,
            "Expected at least one ThrottleProxyFuelUsed emission once k is learned.");

        var persisted = store.Load();
        Assert.IsNotNull(persisted);
        Assert.IsFalse(double.IsNaN(persisted!.K), "k should be learned and persisted.");
        Assert.AreEqual(1, persisted.KSampleCount);

        await consumer.DisposeAsync();
    }

    [TestMethod]
    public async Task Manual_override_takes_precedence_over_learned_k()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 24, 0, 0, 0, TimeSpan.Zero));
        var config = BuildConfig();
        var path = NewTempPath();

        // Seed the store with k=0.001 + manual override = 0.002 BEFORE consumer construction.
        var seed = ThrottleProxyCalibrationState.Empty(8000);
        seed.K = 0.001;
        seed.KSampleCount = 5;
        seed.ManualOverride = new ManualOverrideRecord
        {
            Value = 0.002,
            Source = CalibrationSource.ManualOverride,
            AtUtc = time.GetUtcNow().UtcDateTime,
        };
        new ThrottleProxyCalibrationStore(path).Save(seed);

        var consumer = BuildConsumer(time, config, BuildCarConfig(), FastOptions(path), out var injector, out _);
        _ = consumer.GetSubscriptions(config);
        await consumer.StartAsync(default);
        await Task.Delay(50, TestContext.CancellationToken);

        // Drive a known integral: 100s at 50% TPS = 5000 %·s.
        for (var i = 0; i < 1000; i++)
        {
            var ticks = S(i * 0.1);
            await consumer.HandleAsync(new[]
            {
                new InternalChannelValue(TpsId, 50, ticks, time.GetUtcNow().UtcDateTime),
                new InternalChannelValue(RpmId, 4000, ticks, time.GetUtcNow().UtcDateTime),
            }, default);
        }

        time.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(200, TestContext.CancellationToken);

        var fuelUsedEmissions = injector.Emissions.Where(e => e.ChannelId == OutFuelUsedId).ToList();
        Assert.IsGreaterThan(0, fuelUsedEmissions.Count);
        // ~5000 %·s × 0.002 gal/(%·s) = ~10 gal; with k=0.001 it would be ~5 gal.
        var lastFuelUsed = fuelUsedEmissions[^1].Value;
        Assert.IsGreaterThan(8.0, lastFuelUsed, "Manual override 0.002 must produce ~10 gal, not learned 0.001's ~5 gal.");

        await consumer.DisposeAsync();
    }
}
