# ADR-0003: Fuel range computed by a reconciler over concurrent estimators

**Status:** Accepted  
**Date:** 2026-05-23

## Context

A car's remaining fuel range can be estimated from three independent data sources, each with distinct failure modes:

- **ECU `TripFuel` channel** — precise when the driver has remembered to press the reset button after the most recent refuel and the channel is currently reporting; otherwise unavailable or wrong.
- **Fuel flow meter `FuelUsed` channel** — continuously available on equipped cars but susceptible to calibration drift and to transient spikes when air enters the fuel line and blows past the impeller.
- **Manually entered pit-fill volumes** — physically anchored (it's the actual fuel that went into the car) but limited to ±1 gallon accuracy from eyeballing fuel jugs, dependent on the engineer remembering to enter the value, and only produces a consumption rate after at least one FuelWindow has closed.

An earlier framing in CONTEXT.md treated these as two selectable per-car **modes** ("Fuel-used mode" vs "Volume-entry mode"). The engineer would pick one path per car.

Two options were considered:

- **Option A: Selectable modes.** Engineer picks a single source of truth per car; other sources are unused. Simple data flow; engineer carries responsibility for choosing the right one and switching when their pick fails.
- **Option B: Concurrent estimators with a reconciler.** All available sources run simultaneously as **Fuel Estimators**. A **Fuel Reconciler** produces a single `FuelRangeSnapshot` carrying a primary range, a per-estimator breakdown, confidence intervals, and outlier flags. Outlier detection cross-validates the estimators against each other so a misbehaving sensor is flagged rather than silently trusted.

## Decision

**Option B** — concurrent estimators with a reconciler.

## Rationale

- The estimators have **distinct, non-correlated failure modes** (driver forgot to reset ECU; air bubble in flow meter; engineer forgot to enter jug volume). Cross-validation between them is the only way to catch silent failures during a race when there is no opportunity to manually verify.
- **Estimator availability is a runtime property**, not a configuration choice. Whether `TripFuel` is currently reporting, whether the engineer has entered the latest fill, whether the flow meter has produced calibration data — these change moment to moment. A "pick one mode" config setting cannot represent the actual state.
- **Reconciliation produces a confidence interval that tightens when estimators agree.** This enables a separate "high-confidence range" output that is the conservative number engineers use for pit-call decisions — fundamentally not possible with a single-source design.
- The reconciler design **degrades gracefully**: when only one estimator is available (e.g., during a telemetry disconnect, only PitFill works), the snapshot still emits with that estimator's confidence interval and the others marked unavailable with a reason. The UI does not need a separate "no data" path.

## Consequences

- The data flow is more complex than Option A: every estimator runs every tick, the reconciler runs after them, and the UI must render a primary value alongside a per-estimator breakdown.
- The UI must build engineer trust by exposing the breakdown, not hiding it. A `FuelRangeSnapshot` detail panel shows each estimator's current value, confidence, and outlier status; an opaque "37 minutes" alone would lose trust the first time it disagreed with the engineer's intuition.
- The reconciler emits both a **primary range** (inverse-variance-weighted blend of non-outlier estimators) and a **high-confidence range** at a configurable threshold (default 98%). Headline scalars are published as reserved channels; the full structured snapshot lives in Redis and is fetched via a dedicated WebApi endpoint.
- Estimators are first-class components with explicit availability, confidence-interval, and outlier-flag contracts. Adding a fourth estimator (e.g., a future `FuelLevel`-based estimator, or a learned throttle-position-based proxy) is a matter of implementing the same interface; no UI or reconciler restructure is required.
