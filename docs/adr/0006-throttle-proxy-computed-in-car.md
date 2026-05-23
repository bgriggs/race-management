# ADR-0006: Throttle-position consumption proxy is computed in-car, not in the cloud reconciler

**Status:** Accepted  
**Date:** 2026-05-23

## Context

The Fuel Reconciler (ADR-0003) gains a fourth estimator: a throttle-position consumption proxy with two sub-outputs — a learned integral scalar (`k × ∫TPS dt`) and a 10×10 `(TPS, RPM)` alpha-N lookup grid. Both need continuous sampling of `ThrottlePosition` and `EngineRPM`, and both calibrate themselves against ECU `TripFuel` and FlowMeter `FuelUsed` ground truth measured over local calibration windows.

The other three estimators (ECU, FlowMeter, PitFill) all run inside the cloud ChannelProcessor's reconciler worker. They consume the existing `telemetry` Redis Stream and their inputs are already change-filtered and deadband-quantized by the in-car pipeline before reaching the cloud. The natural question is whether the throttle proxy should join them there or live elsewhere.

Two options were considered:

- **Option A: Cloud-side computation in ChannelProcessor.** A fifth worker (or an extension of the FuelReconciler worker) subscribes to TPS, RPM, TripFuel, FuelUsed, and FuelFull from the telemetry stream. Per-car `k` and grid state live in Redis via `IStateRepository`. The reconciler reads its own estimator outputs from the same in-memory state. Mirrors the existing pattern for the other estimators.
- **Option B: In-car computation in Racecar.** A new `ThrottleProxyConsumer` joins the in-car pipeline alongside `SignalRTransmitConsumer`, subscribing to the same channels at native CAN-arrival rate. Calibration `k`, the grid, and the in-flight integrator accumulator all live on the car in `/etc/racecar/fuel-calibration.json`. The car publishes three derived channels (`ThrottleProxyFuelUsedTotal`, `ThrottleProxyRateGalPerMin`, `ThrottleProxyConfidence`) into the telemetry stream; the cloud reconciler consumes them like any other channel value and treats the throttle proxy as an estimator with two sub-outputs.

## Decision

**Option B** — the throttle proxy is computed in-car. The cloud reconciler consumes its outputs as a fourth estimator but does not perform the sampling, integration, or calibration learning itself.

A new Postgres table `ThrottleProxyCalibrations` is added as a recovery-grade snapshot stream pushed by the car on each closed calibration window. The car's local file is the authoritative copy.

## Rationale

- **Sampling fidelity is the entire point of the estimator.** The in-car pipeline sees raw CAN-decoded TPS and RPM values at native bus rate (hundreds to thousands of samples per second) before the change-filter and deadband. The cloud, by design, sees only change-filtered values at the SignalR transmit cadence (100 ms delta loop). For an integrator and a grid binner, every quantization step is integral error. The throttle proxy is the only fuel estimator whose accuracy is dominated by sample density rather than by sensor precision — this is exactly the workload that belongs on the edge.
- **Ground truth lives next to the integrator.** ECU `TripFuel` and FlowMeter `FuelUsed` resets, and `FuelFull` assertions, all originate on the car. Calibration window open/close detection and the corresponding `Δfuel` accumulation are tightest when they happen in the same pipeline as the source channels — no double-network-hop, no telemetry-disconnect race conditions between "calibration window opened" and "calibration data arriving."
- **Disconnect resilience.** A car in cellular dropout continues to learn its own calibration; nothing is lost. Cloud-side computation would simply stop learning whenever any car disconnected, and the multi-hour gaps common in endurance racing would meaningfully degrade the learned model. The proxy is the *only* estimator whose learning loop is fully self-contained on the car.
- **Cloud ChannelProcessor stays stateless and horizontally scalable.** A per-car grid (100 doubles × N cars) plus per-car integrator state plus per-car calibration window state is meaningful state — and unlike the existing reconciler state (which is already in Redis behind `IStateRepository`), it is high-write-rate (every TPS sample). Pushing this into the cloud would either force sticky-by-car routing (breaking ADR-0002's "any replica can process any car" design) or impose a hot Redis write path that the existing reconciler does not have.
- **Symmetry with the FlowMeter calibration is only superficial.** FlowMeter calibration is a single scalar updated infrequently (once per closed FuelWindow); its inputs are aggregated values, not raw samples. Co-locating it with the reconciler is appropriate. The throttle proxy is continuous, high-rate, and stateful by nature — a different shape of computation that warrants a different home.
- **The car already owns analogous self-management responsibilities.** The Racecar core app already loads, persists, and atomically swaps its own configuration; the update agent already manages binaries and rollbacks. Adding a small atomic-rename JSON store for learned parameters is a precedent-following extension, not a new architectural surface.

## Consequences

- **Cloud visibility comes from snapshots, not from the live computation.** The cloud sees `ThrottleProxyCalibrations` rows written at each closed calibration window plus the three continuous output channels (`ThrottleProxyFuelUsedTotal`, `ThrottleProxyRateGalPerMin`, `ThrottleProxyConfidence`) in the telemetry stream. Mid-window state (current `throttleIntegral` accumulator, per-cell partial sums) is not snapshotted. If the car's local file is lost mid-window, the in-flight learning for that window is lost; the calibration restarts from the most recent snapshot on next start. A future enhancement could re-derive mid-window state from `ChannelLogs` for incident recovery, but this is not part of v1.
- **Engineer overrides round-trip to the car.** The cloud-side `WebApi → /api/cars/{carId}/throttle-calibration/override` endpoint must reach the in-car authoritative store. When the car is connected, the cloud publishes a command channel value with `sendToCar=true`, which CarGateway forwards (the existing one-way command path). When the car is local-only, the pit-laptop's REST proxy hits the management API on the car directly. The cloud database holds the override intent as the most recent `ThrottleProxyCalibrations` row with `Source = manual_override`, but the car is the source of truth — and reconciliation on reconnect favors the car's value if the two diverge (it is what the proxy is actually using).
- **`ReservedChannels.cs` grows by six entries** — `ThrottlePosition`, `EngineRPM`, and the four `ThrottleProxy*` channels. `ThrottlePosition` and `EngineRPM` are car-side inputs that were already de facto reserved (every car configuration carries them); making them formally reserved is overdue regardless of this decision.
- **`CarFuelConfigs` gains an `RpmMax` column** so the in-car module can size its 10 RPM bins per car. This is a static per-car property (set once at car commissioning) and not a learned value.
- **The reconciler's outlier debounce now spans two estimators that share a single physical source.** `throttle.integral` and `throttle.grid` are both derived from the same `(TPS, RPM)` stream and will tend to fail together (e.g., if the TPS sensor faults). The inverse-variance-weighted blend handles this correctly — when both go outlier, their combined weight drops — but operators should be aware that "two estimators agree" is weaker evidence when those two share a source. The detail panel renders them as two lines so this is visible rather than hidden.
- **Testability is preserved.** `ThrottleProxyConsumer` follows the existing `IChannelConsumer` contract and is testable against the same fake `ICanBus` + `FakeTimeProvider` rig used for the rest of the pipeline (per the Racecar Testing section). The calibration store has its own focused tests against the atomic-rename persistence contract.

## References

Internal:
- [ADR-0002](0002-channelprocessor-redis-evaluator-state.md) — establishes the "any replica processes any car" invariant for ChannelProcessor; the in-car decision here preserves that invariant by keeping per-car throttle-proxy state off the cloud.
- [ADR-0003](0003-fuel-reconciler-over-concurrent-estimators.md) — the reconciler-over-concurrent-estimators design that the throttle proxy plugs into as a fourth estimator.
- [ADR-0004](0004-ecu-primary-fuel-ground-truth.md) — ECU `TripFuel` as ground truth, which the in-car calibration windows consume on every closed window.
- [design.md — Fuel Analysis](../../design.md#fuel-analysis) — full feature design including the `ThrottleProxy Estimator` and `In-Car Throttle Proxy Module` sections.

External (informed the technical approach):
- Alpha-N fueling strategy — [Holley: Fuel Injection Fundamentals — Three Fueling Strategies](https://www.holley.com/blog/post/fuel_injection_fundamentals_understanding_the_three_different_fueling_strategies/). Establishes the precedent for `(TPS, RPM)` 2D lookup as a viable fuel-flow estimator when MAP/MAF are unavailable or unreliable — the same conditions we face when working from CAN-broadcast channels rather than raw ECU internals.
- HP Academy — [Throttle-Position / Alpha-N Tuning with a Turbo and Individual Throttle Bodies](https://www.hpacademy.com/technical-articles/throttle-positionalpha-n-tuning-with-a-turbo-and-individual-throttle-bodies/). Practitioner-level discussion of alpha-N's accuracy bounds and the regimes where TPS-alone breaks down — informed the choice to run the integral and grid as two separate sub-outputs rather than one.
- Liaqat et al., *Optimizing Fuel Consumption Prediction Without an On-Board Diagnostic System* (PMC, 2025) — [pmc.ncbi.nlm.nih.gov/articles/PMC12656463](https://pmc.ncbi.nlm.nih.gov/articles/PMC12656463/). LSTM achieving R² = 0.95 on `{speed, accel, TPS, dTPS/dt}` at 1 Hz; demonstrates that throttle-derived signals carry sufficient information for fuel-consumption prediction without MAF/MAP. Reviewed and explicitly rejected in favor of the simpler grid approach — the marginal accuracy did not justify the operational complexity of an in-car ML model — but the paper validates the underlying premise.
- BSFC framework — [x-engineer.org: Brake Specific Fuel Consumption](https://x-engineer.org/brake-specific-fuel-consumption-bsfc/). Underlies the assumption that fuel mass flow at a given `(TPS, RPM)` is repeatable enough to be table-lookable — the physics justification for the alpha-N grid.
