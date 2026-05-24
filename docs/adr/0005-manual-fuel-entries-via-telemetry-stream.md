# ADR-0005: Manual fuel entries flow through the telemetry stream as channel values

**Status:** Accepted  
**Date:** 2026-05-23

## Context

Two pieces of fuel-related data are entered by the engineer through the WebApi-served UI rather than originating in the car:

- **Manual fuel-jug volume** entered at a Refuel Event (the PitFill estimator's primary input).
- **Race flag state** — currently sourced from the RedMist timing system integration, but conceptually any externally observed condition (engineer-entered manual flag override, future timing-system integrations) emits this signal.

Both feed the Fuel Reconciler running in ChannelProcessor. Two integration paths were considered:

- **Option A: WebApi writes directly to Postgres.** The fuel entry endpoint updates `RefuelEvents.EnteredFuelGallons`; ChannelProcessor's FuelReconciler worker watches Postgres (via polling or a notification channel) and reacts to changes.
- **Option B: WebApi publishes to the telemetry stream as a channel value.** The fuel entry endpoint publishes a `ManualFuelAddedGallons` Reserved Channel value (with `source = "user:webapi"`) to the existing `telemetry` Redis Stream. The FuelReconciler consumes it through its normal stream consumer just like any other channel value, and writes the volume back to `RefuelEvents.EnteredFuelGallons` as part of its handler logic.

## Decision

**Option B** — manual fuel entries (and race flag state) flow through the telemetry stream as channel values.

Two new Reserved Channels are added:
- `ManualFuelAddedGallons` (Volume / UsGallon)
- `RaceFlagState` (string enum: `Green`, `Yellow`, `Code60`, `Code35`, `Red`)

## Rationale

- **Single input contract for the reconciler.** The FuelReconciler consumes the telemetry stream — that is its only input. It does not need a second code path for Postgres notifications, polling, or out-of-band events. Every input the reconciler reasons about (sensor values, manual entries, flag state) arrives via the same channel-value abstraction.
- **Reuses the existing pattern.** The design already treats cloud-sourced channel values as first-class: the `source` field on stream messages exists precisely so that cloud-origin values can flow through the pipeline alongside car-origin ones, with the same `(carId, channelId, value, timestamp)` shape. Manual fuel entries are exactly this shape — there is no reason to invent a parallel path.
- **Free audit and replay.** Manual entries are automatically logged to `ChannelLogs` by the existing ChannelLogger worker — no separate audit table needed. The full history of who entered what fuel volume and when is queryable through the same channel-log queries used for any other data.
- **Backdated entries work without special handling.** The engineer can adjust the timestamp on a refuel entry (per CONTEXT.md's existing requirement that engineers may record a pit stop with a past timestamp). The published channel value simply carries the adjusted timestamp; no separate "backdated entry" code path is needed in WebApi or the reconciler.
- **Cleaner failure semantics.** If WebApi crashes after publishing but before the reconciler processes, the stream message is durable and gets handled on next read. A direct table-write path would require either a transactional outbox or accepting silent data loss in this window.

## Consequences

- WebApi gains a stream-publisher responsibility for these channel values, in addition to its existing direct Postgres writes for CRUD entities. This is a narrow exception (limited to the two channels above) and matches the pattern of any other cloud-origin telemetry source.
- The `ReservedChannels.cs` list grows by these two channels — and by the Reconciler's emitted output channels (`FuelRangeMinutes`, `FuelRangeMinutesHighConf`, etc.) which are emitted by ChannelProcessor for the same architectural reason. This is the expected pattern, not new surface area.
- A future "manual flag-state override" UI (engineer overrides the auto-detected `RaceFlagState`) is trivial — same WebApi pattern, same channel.
- The Reconciler's `RefuelEvents.EnteredFuelGallons` write happens as part of its message-handler logic and is subject to the same ACK-after-state-write rule (ADR-0002): the stream message is not ACKed until the Postgres write completes, so a mid-message pod termination causes redelivery and idempotent reprocessing.
