using Common;
using Common.FuelAnalysis;
using Microsoft.Extensions.Options;
using Racecar.Pipeline;
using Racecar.Pipeline.Dispatch;

namespace Racecar.FuelAnalysis;

/// <summary>
/// In-car throttle proxy module. Subscribes to <c>ThrottlePosition</c>,
/// <c>EngineRPM</c>, <c>TripFuel</c>, <c>FuelUsed</c>, <c>FuelFull</c>, and
/// <c>InPit</c> from the pipeline; maintains the throttle integral, the
/// alpha-N grid, and the calibration window; and emits the four reserved
/// <c>ThrottleProxy*</c> channels back into the pipeline via
/// <see cref="IDerivedChannelInjector"/> on a 1 Hz cadence (30 s for grid
/// coverage). Persists learned state to <c>fuel-calibration.json</c> with
/// atomic-rename writes.
/// </summary>
public sealed class ThrottleProxyConsumer
    : IChannelConsumer, IHostedService, IAsyncDisposable
{
    // Output channel GUIDs — kept in sync with ReservedChannels.cs by id, not by name.
    // Inputs are now resolved via ThrottleConsumptionConfig (user-configurable per-config) — see InputChannelMap.Build.
    private static readonly Guid ThrottleProxyFuelUsedGuid = Guid.Parse("916d4b4f-5bcf-4d9c-2a1e-4d6e8b3c5a0d");
    private static readonly Guid ThrottleProxyRateGuid = Guid.Parse("a27e5c50-6cd1-4eac-3b2f-5e7f9c4d6b0e");
    private static readonly Guid ThrottleProxyConfidenceGuid = Guid.Parse("b38f6d61-7de2-4fbd-4c3a-6f8a1d5e7c0f");
    private static readonly Guid ThrottleProxyGridCoverageGuid = Guid.Parse("c49a7e72-8ef3-4ace-5d4b-7a9b2e6f8d10");

    private readonly Func<IDerivedChannelInjector> _injectorAccessor;
    private readonly Func<ActiveConfiguration> _configAccessor;
    private readonly IOptionsMonitor<CarConfiguration> _carConfig;
    private readonly FuelAnalysisOptions _options;
    private readonly ThrottleProxyCalibrationStore _store;
    private readonly TimeProvider _time;
    private readonly ILogger<ThrottleProxyConsumer> _logger;

    private readonly Lock _stateLock = new();

    private InputChannelMap? _inputs;
    private readonly ThrottleProxyCalibrationState _calibration;
    private readonly ThrottleIntegralTotalizer _totalizer;
    private readonly AlphaNGrid _grid;
    private readonly CalibrationWindow _window = new();
    private readonly EcuResetDetector _detector;

    private double _lastTps;
    private double _lastRpm;
    private double _lastTripFuel;
    private bool _inPit;

    // 2-second EMA of the looked-up cell rate for ThrottleProxyRate.
    private double _smoothedRateGalPerMin = double.NaN;
    private long? _lastRateSampleTicks;

    private double _lastEmittedFuelUsed = double.NaN;
    private double _lastEmittedRate = double.NaN;
    private double _lastEmittedConfidence = double.NaN;
    private double _lastEmittedCoverage = double.NaN;

    private CancellationTokenSource? _cts;
    private Task? _emitLoop;
    private Task? _coverageLoop;
    private IDisposable? _carConfigSubscription;
    private bool _lastEnabledState;

    public ThrottleProxyConsumer(
        Func<IDerivedChannelInjector> injectorAccessor,
        Func<ActiveConfiguration> configAccessor,
        IOptionsMonitor<CarConfiguration> carConfig,
        IOptions<FuelAnalysisOptions> options,
        ThrottleProxyCalibrationStore store,
        TimeProvider time,
        ILogger<ThrottleProxyConsumer> logger,
        ChannelConsumerOptions? consumerOptions = null)
    {
        _injectorAccessor = injectorAccessor;
        _configAccessor = configAccessor;
        _carConfig = carConfig;
        _options = options.Value;
        _store = store;
        _time = time;
        _logger = logger;
        Options = consumerOptions ?? new ChannelConsumerOptions();

        var rpmMax = ResolveRpmMax(carConfig.CurrentValue);
        _calibration = LoadOrSeed(rpmMax);
        _totalizer = new ThrottleIntegralTotalizer(_options);
        _grid = new AlphaNGrid(rpmMax, _calibration);
        _detector = new EcuResetDetector(_options);
        _lastEnabledState = ComputeEnabled(carConfig.CurrentValue);
    }

    /// <summary>True when both the fuel and throttle-consumption configs opt in to running this module.</summary>
    public bool IsEnabled => ComputeEnabled(_carConfig.CurrentValue);

    private static bool ComputeEnabled(CarConfiguration cfg) =>
        cfg.FuelConfig.IsEnabled && cfg.FuelConfig.ThrottleConsumption.IsEnabled;

    public string Name { get; } = "throttle-proxy";
    public ChannelConsumerOptions Options { get; }

    public IReadOnlySet<int>? GetSubscriptions(ActiveConfiguration config)
    {
        if (!IsEnabled)
        {
            Interlocked.Exchange(ref _inputs, null);
            return new HashSet<int>();
        }
        var inputs = InputChannelMap.Build(config, _carConfig.CurrentValue.FuelConfig);
        Interlocked.Exchange(ref _inputs, inputs);
        return inputs.AllIds;
    }

    public ValueTask HandleAsync(ReadOnlyMemory<InternalChannelValue> values, CancellationToken ct)
    {
        if (!IsEnabled) return ValueTask.CompletedTask;
        var inputs = Volatile.Read(ref _inputs);
        if (inputs is null) return ValueTask.CompletedTask;

        var span = values.Span;
        lock (_stateLock)
        {
            for (var i = 0; i < span.Length; i++)
            {
                ref readonly var v = ref span[i];
                if (v.ChannelId == inputs.ThrottlePosition) _lastTps = v.Value;
                else if (v.ChannelId == inputs.EngineRpm) _lastRpm = v.Value;
                else if (v.ChannelId == inputs.TripFuel)
                {
                    _lastTripFuel = v.Value;
                    _detector.OnTripFuel(v.Value, v.MonotonicTicks);
                    TryOpenWindow(v.MonotonicTicks);
                }
                else if (v.ChannelId == inputs.FuelUsed) { /* reserved for future flowmeter-corrected ground truth */ }
                else if (v.ChannelId == inputs.FuelFull)
                {
                    _detector.OnFuelFull(v.Value, v.MonotonicTicks);
                    TryOpenWindow(v.MonotonicTicks);
                    TryCloseWindow(v.Value, v.MonotonicTicks);
                }
                else if (v.ChannelId == inputs.InPit)
                {
                    _inPit = v.Value > 0.5;
                }
                else
                {
                    continue;
                }

                if (v.ChannelId == inputs.ThrottlePosition || v.ChannelId == inputs.EngineRpm)
                {
                    SampleTotalizerAndGrid(v.MonotonicTicks);
                }
            }
        }
        return ValueTask.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _emitLoop = Task.Run(() => EmitLoopAsync(_cts.Token), cancellationToken);
        _coverageLoop = Task.Run(() => CoverageLoopAsync(_cts.Token), cancellationToken);
        _carConfigSubscription = _carConfig.OnChange(OnCarConfigChanged);
        _logger.LogInformation(
            "Throttle proxy module started ({State}).", _lastEnabledState ? "enabled" : "disabled");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken) => await DisposeAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        if (_cts is null) return;
        _carConfigSubscription?.Dispose();
        _carConfigSubscription = null;
        _cts.Cancel();
        try
        {
            if (_emitLoop is not null) await _emitLoop.ConfigureAwait(false);
            if (_coverageLoop is not null) await _coverageLoop.ConfigureAwait(false);
        }
        catch { /* shutdown */ }

        try
        {
            lock (_stateLock) _store.Save(_calibration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Throttle proxy calibration save on shutdown failed.");
        }

        _cts.Dispose();
        _cts = null;
    }

    // ----- Internal -----

    private void OnCarConfigChanged(CarConfiguration cfg)
    {
        var nowEnabled = ComputeEnabled(cfg);
        bool wasEnabled;
        lock (_stateLock)
        {
            wasEnabled = _lastEnabledState;
            if (wasEnabled == nowEnabled) return;
            _lastEnabledState = nowEnabled;
            // On enabled→disabled, drop transient accumulators so the next enable
            // doesn't apply a stale TPS integral against fresh TripFuel deltas.
            // Persisted k and grid in _calibration are preserved.
            if (!nowEnabled)
            {
                _totalizer.Reset();
                _window.Discard();
                _grid.ResetWindowObservations();
            }
        }
        _logger.LogInformation(
            "Throttle proxy module {State}.", nowEnabled ? "enabled" : "disabled");
    }

    private void SampleTotalizerAndGrid(long monotonicTicks)
    {
        var attribution = _totalizer.OnSample(_lastTps, _lastRpm, monotonicTicks);
        if (attribution.HasInterval && _window.IsOpen)
        {
            _grid.RecordWindowSample(attribution.PrevTps, attribution.PrevRpm, attribution.DeltaSeconds);
        }
    }

    private void TryOpenWindow(long monotonicTicks)
    {
        if (!_detector.ResetJustDetected) return;
        if (_window.IsOpen) return;
        _grid.ResetWindowObservations();
        _window.Open(_lastTripFuel, _totalizer.Integral, monotonicTicks);
        _logger.LogInformation("Throttle proxy calibration window opened (TripFuel={Trip:0.00} gal).", _lastTripFuel);
    }

    private void TryCloseWindow(double? currentFuelFullValue, long monotonicTicks)
    {
        if (!_window.IsOpen) return;
        var fuelFullAsserted = currentFuelFullValue is double v && v > 0.5;
        if (!fuelFullAsserted || !_inPit) return;

        var observation = _window.Close(_lastTripFuel, _totalizer.Integral, monotonicTicks);
        if (observation is null)
        {
            _logger.LogInformation("Throttle proxy calibration window discarded (no usable Δfuel / Δintegral).");
            return;
        }

        ApplyObservation(observation.Value);
    }

    private void ApplyObservation(CalibrationObservation obs)
    {
        // Scalar k update: gal per (TPS-% · s).
        var kObserved = obs.DeltaFuelGallons / obs.DeltaIntegral;
        if (double.IsNaN(_calibration.K))
        {
            _calibration.K = kObserved;
        }
        else
        {
            var a = _options.LearningEmaAlpha;
            _calibration.K = (1 - a) * _calibration.K + a * kObserved;
        }
        _calibration.KSampleCount++;
        _calibration.KLastUpdatedUtc = _time.GetUtcNow().UtcDateTime;

        _ = _grid.ApplyWindowClose(obs.DeltaFuelGallons, obs.DeltaIntegral, _options.LearningEmaAlpha);
        _calibration.Grid = _grid.Snapshot();

        try
        {
            _store.Save(_calibration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Throttle proxy calibration save failed.");
        }

        _logger.LogInformation(
            "Throttle proxy calibration window closed: ΔFuel={Fuel:0.00} gal Δint={Int:0.0} (%·s) → k_obs={KObs:0.000000} k={K:0.000000} (n={N}).",
            obs.DeltaFuelGallons, obs.DeltaIntegral, kObserved, _calibration.K, _calibration.KSampleCount);
    }

    private async Task EmitLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(_options.EmitInterval, _time, ct).ConfigureAwait(false);
                EmitTick();
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task CoverageLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(_options.CoverageEmitInterval, _time, ct).ConfigureAwait(false);
                EmitCoverage();
            }
        }
        catch (OperationCanceledException) { }
    }

    private void EmitTick()
    {
        if (!IsEnabled) return;
        var nowTicks = _time.GetTimestamp();
        var wall = _time.GetUtcNow().UtcDateTime;

        double k;
        double integral;
        double cellRate;
        int cellSamples;
        int kSampleCount;
        lock (_stateLock)
        {
            _detector.Tick(nowTicks);
            k = _calibration.ManualOverride?.Value ?? _calibration.K;
            integral = _totalizer.Integral;
            cellRate = _grid.Lookup(_lastTps, _lastRpm);
            cellSamples = _grid.GetCellSampleCount(_grid.CellIndex(_lastTps, _lastRpm));
            kSampleCount = _calibration.KSampleCount;
        }

        var emissions = new List<InternalChannelValue>(3);

        if (!double.IsNaN(k))
        {
            var fuelUsedGal = k * integral;
            EmitIfChanged(emissions, ThrottleProxyFuelUsedGuid, fuelUsedGal, ref _lastEmittedFuelUsed, nowTicks, wall);
        }

        var smoothed = UpdateRateEma(cellRate, nowTicks);
        if (!double.IsNaN(smoothed))
        {
            EmitIfChanged(emissions, ThrottleProxyRateGuid, smoothed, ref _lastEmittedRate, nowTicks, wall);
        }

        var confidence = ComputeConfidence(kSampleCount, cellSamples) * 100.0;
        EmitIfChanged(emissions, ThrottleProxyConfidenceGuid, confidence, ref _lastEmittedConfidence, nowTicks, wall);

        if (emissions.Count > 0) PushDerived(emissions);
    }

    private void EmitCoverage()
    {
        if (!IsEnabled) return;
        var nowTicks = _time.GetTimestamp();
        var wall = _time.GetUtcNow().UtcDateTime;
        double coverage;
        lock (_stateLock)
        {
            coverage = _grid.Coverage(_options.MinCellSamples) * 100.0;
        }
        var emissions = new List<InternalChannelValue>(1);
        EmitIfChanged(emissions, ThrottleProxyGridCoverageGuid, coverage, ref _lastEmittedCoverage, nowTicks, wall);
        if (emissions.Count > 0) PushDerived(emissions);
    }

    private double UpdateRateEma(double cellRate, long nowTicks)
    {
        if (double.IsNaN(cellRate)) return _smoothedRateGalPerMin;

        if (double.IsNaN(_smoothedRateGalPerMin) || _lastRateSampleTicks is null)
        {
            _smoothedRateGalPerMin = cellRate;
        }
        else
        {
            var dt = TimeSpan.FromTicks(nowTicks - _lastRateSampleTicks.Value).TotalSeconds;
            var tau = _options.RateEmaTimeConstant.TotalSeconds;
            if (dt > 0 && tau > 0)
            {
                var alpha = 1.0 - Math.Exp(-dt / tau);
                _smoothedRateGalPerMin = _smoothedRateGalPerMin + alpha * (cellRate - _smoothedRateGalPerMin);
            }
        }
        _lastRateSampleTicks = nowTicks;
        return _smoothedRateGalPerMin;
    }

    private double ComputeConfidence(int kSampleCount, int currentCellSamples)
    {
        var kScore = _options.KConvergenceSampleCount <= 0
            ? 1.0
            : Math.Min(1.0, kSampleCount / (double)_options.KConvergenceSampleCount);
        var cellScore = _options.MinCellSamples <= 0
            ? 1.0
            : Math.Min(1.0, currentCellSamples / (double)_options.MinCellSamples);
        return Math.Min(kScore, cellScore);
    }

    private void EmitIfChanged(
        List<InternalChannelValue> sink,
        Guid guid,
        double value,
        ref double lastEmitted,
        long monotonicTicks,
        DateTime wallTime)
    {
        var config = _configAccessor();
        if (!config.ChannelIdsByDefinition.TryGetValue(guid, out var channelId)) return;
        if (!double.IsNaN(lastEmitted) && value == lastEmitted) return;
        sink.Add(new InternalChannelValue(channelId, value, monotonicTicks, wallTime));
        lastEmitted = value;
    }

    private void PushDerived(List<InternalChannelValue> emissions)
    {
        var span = new InternalChannelValue[emissions.Count];
        for (var i = 0; i < emissions.Count; i++) span[i] = emissions[i];
        _injectorAccessor().InjectDerived(span);
    }

    private ThrottleProxyCalibrationState LoadOrSeed(int rpmMax)
    {
        var loaded = _store.Load();
        if (loaded is null) return ThrottleProxyCalibrationState.Empty(rpmMax);
        if (loaded.Grid.RpmBinMax != rpmMax)
        {
            _logger.LogWarning(
                "Persisted throttle calibration RpmMax {Loaded} differs from current {Current} — discarding grid, retaining k.",
                loaded.Grid.RpmBinMax, rpmMax);
            var fresh = ThrottleProxyCalibrationState.Empty(rpmMax);
            fresh.K = loaded.K;
            fresh.KLastUpdatedUtc = loaded.KLastUpdatedUtc;
            fresh.KSampleCount = loaded.KSampleCount;
            fresh.ManualOverride = loaded.ManualOverride;
            return fresh;
        }
        return loaded;
    }

    private static int ResolveRpmMax(CarConfiguration cfg) =>
        cfg.FuelConfig.ThrottleConsumption.MaxRpm;

    /// <summary>Lookup of pipeline channel IDs for the proxy's six input channels under a given config.</summary>
    private sealed record InputChannelMap(
        int ThrottlePosition,
        int EngineRpm,
        int TripFuel,
        int FuelUsed,
        int FuelFull,
        int InPit,
        IReadOnlySet<int> AllIds)
    {
        public static InputChannelMap Build(ActiveConfiguration config, CarFuelConfig fuelConfig)
        {
            var throttleConfig = fuelConfig.ThrottleConsumption;
            var tps = Resolve(config, throttleConfig.ThrottlePositionChannelId);
            var rpm = Resolve(config, throttleConfig.EngineRpmChannelId);
            var trip = Resolve(config, fuelConfig.TripFuelChannelId);
            var used = Resolve(config, fuelConfig.FuelUsedChannelId);
            var full = Resolve(config, fuelConfig.FuelFullChannelId);
            var pit = fuelConfig.InPitChannelId is Guid pitGuid ? Resolve(config, pitGuid) : -1;

            var ids = new HashSet<int>();
            if (tps >= 0) ids.Add(tps);
            if (rpm >= 0) ids.Add(rpm);
            if (trip >= 0) ids.Add(trip);
            if (used >= 0) ids.Add(used);
            if (full >= 0) ids.Add(full);
            if (pit >= 0) ids.Add(pit);

            return new InputChannelMap(tps, rpm, trip, used, full, pit, ids);
        }

        private static int Resolve(ActiveConfiguration config, Guid guid) =>
            guid != Guid.Empty && config.ChannelIdsByDefinition.TryGetValue(guid, out var id) ? id : -1;
    }
}
