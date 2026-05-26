# ADR-0007: Channel routing is declarative on `ChannelDefinition`, not per-message

**Status:** Accepted  
**Date:** 2026-05-23

## Context

The system has channels that originate at multiple tiers (the car, the cloud reconciler, external integrations like RedMist, engineer-entered values from WebApi) and that need to reach varying combinations of destinations (in-car consumers, cloud reconcilers, browser dashboards, the car itself for command-style values). Examples that exercise the full matrix:

- `CoolantTemp` — car-produced, transmitted to the cloud for dashboards and alarms; not relevant in the car except for the in-car gauge.
- `FuelRangeMinutes` — cloud-produced by the reconciler, consumed by browser dashboards; the car has no use for it.
- `RaceFlagState` — cloud-sourced (RedMist), needs to reach both cloud-side alarm rules and the car (so the car can display the flag and feed it into in-car consumers); applies to every car on the team, not to any one car.
- Pipeline diagnostic counters in the in-car pipeline (e.g., `pipeline.consumer.alarm.dropped`) — useful to the in-car watchdog and the pit-laptop dashboard, but not worth transmitting to the cloud.
- Calibration-override commands sent from the cloud to a specific car — cloud-produced, must reach exactly that car.

An earlier design (per the pre-this-ADR text of `design.md` and `CONTEXT.md`) attempted to handle this with a per-message `sendToCar` boolean on every `telemetry` stream record: any cloud service publishing a value would set the flag if it wanted CarGateway to forward the value to the car. Three problems showed up before any of this was implemented in code:

1. **Routing was a per-publish concern, not a property of the channel.** Every cloud service had to remember to set the right flag every time it published. A future reconciler change could silently start sending `FuelRangeMinutes` to cars because someone flipped the wrong boolean during an unrelated refactor. There was no declarative source of truth.
2. **Only one direction was modeled.** Cloud→car. The mirror question — *should this car-side channel even be transmitted to the cloud?* — had no answer at all. In-car diagnostic counters either had to fight to stay off the cloud transmit list via ad-hoc filtering, or were silently uplinked, wasting bandwidth and noise on dashboards.
3. **No notion of team-scoped values.** `RaceFlagState` could either be replicated per-car on the stream (wasteful, conceptually muddled — the same value published N times with N different `carId`s) or smuggled outside the channel system entirely (losing the dashboard, alarm, and logging uniformity that makes channels useful in the first place).

The discussion that led to this ADR considered three shapes for a fix:

- **Option A: Keep `sendToCar`, add a sibling per-message `transmitToCloud` flag.** Symmetric but inherits all the per-publish-discipline problems.
- **Option B: Two new fields on `ChannelDefinition` — `Origin` (`Car` / `Cloud`) plus two booleans `TransmitToCloud` and `TransmitToCar`.** Declarative, but the four-combination space includes invalid combinations (Origin=Car + TransmitToCloud=false + TransmitToCar=true makes no sense) that would need to be either runtime-validated or documented away.
- **Option C: A single `Distribution` enum on `ChannelDefinition` encoding the four meaningful combinations directly (`CarLocal`, `CarToCloud`, `CloudLocal`, `CloudToCar`), plus an orthogonal `Scope` enum (`PerCar`, `PerTeam`), plus a `ManagedByFeature` nullable string identifying which feature owns the channel's lifecycle.**

## Decision

**Option C.** Three new fields on `ChannelDefinition`:

- `ChannelDistribution Distribution` — `CarLocal`, `CarToCloud` (default), `CloudLocal`, `CloudToCar`. Drives routing: the in-car `SignalRTransmitConsumer` skips `CarLocal` values; CarGateway forwards `CloudToCar` values back to the car (per-car for `Scope=PerCar`, fan-out to every connected car on the team for `Scope=PerTeam`).
- `ChannelScope Scope` — `PerCar` (default) or `PerTeam`. Determines which stream and state store a value flows through: `PerCar` values use the existing `car-channel-values` Redis Stream keyed by `carKey` and the `car-channels:{carKey}` state hash; `PerTeam` values use a parallel `team-channel-values` stream keyed by `team-{teamId}` and a `team-channels:{teamId}` state hash. `PerTeam` reserved channels still appear in every car's `channelDefinitions` list (so the in-car pipeline can resolve incoming team values to a local SessionIndex), but the cloud-side stream and state representation is independent of any specific car. See the **Implementation notes** below for the stream/payload split.
- `string? ManagedByFeature` — nullable identifier (e.g., `"fuel-analysis"`, `"throttle-consumption"`) of the feature that owns the channel's lifecycle. On reserved-channel templates, this marks the channel as auto-injected when the feature is enabled, hidden from the user's reserved-channel picker, and removed when the feature is disabled. The value propagates to per-car channel instances at injection time, where the UI uses it to lock editing and deletion.

The per-message `sendToCar` flag is **removed** from the telemetry stream message format entirely.

## Rationale

- **Single source of truth.** A channel's distribution is defined once, at the channel level, and read by every consumer that needs to make a routing decision. New cloud services that publish values cannot accidentally forget to set a flag — there is no flag to set.
- **Symmetric in both directions.** `CarLocal` and `CloudLocal` are first-class declarative answers to "this value stays here." The in-car pipeline can fan a diagnostic counter to in-car consumers (alarms, watchdog, pit-laptop dashboard) and skip the cloud transmit without ad-hoc filtering at every consumer.
- **Scope is a separate axis.** Conflating "where the value is produced" with "what entity it applies to" forces awkward modeling for `RaceFlagState`. Splitting `Scope` out keeps `Distribution` orthogonal — a `PerTeam` channel can be any of the four distribution values; a `PerCar` channel can be too. The matrix is clean.
- **`ManagedByFeature` collapses three otherwise-separate concerns into one field.** Picker filtering, instance lock state, and feature-driven lifecycle (auto-inject/auto-remove) all derive from the same identifier. Implementations elsewhere in the codebase tend to grow three parallel flags for these — one field is simpler and impossible to get out of sync.
- **The enum captures only the meaningful combinations.** Option B's two-boolean form has a "Origin=Car + TransmitToCloud=false + TransmitToCar=true" cell that can be drawn but cannot be operationalized. A four-value enum makes the invalid combinations un-representable.
- **`PerTeam` reserved channels in v1 are static.** The only `PerTeam` channel in v1 is `RaceFlagState`, defined in `ReservedChannels.cs`. There is no user-facing Team Channels editor yet, so `PerTeam` values do not need a `TeamChannelDefinitions` table or its surrounding plumbing. The schema field is in place; the editor will follow when a second `PerTeam` use case lands.

## Consequences

- **CarGateway resolves channel definitions via a process-local cache keyed by `configurationId`.** Each car's `car-active-config:{carKey}` Redis pointer is read on every forwarding decision and resolves to the immutable configuration Guid the car is currently running; a new edit produces a new configurationId, the pointer flips on the car's next `SendChannelValuesAsync`, and the old cache entry simply becomes unreachable. No `car-events` subscription is required for invalidation. The cached value exposes two views built from the same configuration: a forward map (`SessionIndex → ChannelDefinition`) for `CloudToCar` filtering on `car-channel-values`, and a reverse map (`ChannelId → SessionIndex`) used during team fan-out to re-index a `TeamChannelValue` for each receiving car. Cache miss falls back to a Postgres lookup; a missing definition is logged and the value is dropped.
- **`ChannelLogs` storage for `PerTeam` channels is deferred.** The v1 logger writes only `PerCar` rows. When `PerTeam` history queries become a requirement, the split mirrors the stream split: either a parallel `TeamChannelLogs` table keyed by `(TeamId, ChannelId, Timestamp)`, or `ChannelLogs.CarId` made nullable with an additional `(TeamId, ChannelId, Timestamp)` index. The choice can be made when the logger gains `team-channel-values` consumption; nothing in v1 routing depends on it.
- **In-car `SignalRTransmitConsumer` filters by `Distribution != CarLocal`** at the transmit boundary. `CarLocal` values still flow through the in-car pipeline for alarms, dashboards, and the pit-laptop snapshot endpoint — they just don't cross the cloud uplink.
- **Reserved-channel picker filtering.** The Edit Channel UI's reserved-channel picker hides any reserved channel whose `ManagedByFeature` is non-null. Users who want such a channel must enable its owning feature; the feature's configuration handler auto-injects it into the car's `channelDefinitions` list.
- **Channel-instance lock state.** The Channels list renders rows with `ManagedByFeature != null` in a locked/read-only state (lock icon + badge indicating the owning feature). Edit and delete actions are disabled for those rows.
- **Feature-driven lifecycle.** When `CarFuelConfig.IsEnabled` (or `ThrottleConsumption.IsEnabled`) flips true, the system auto-adds the corresponding reserved channels to the car's `channelDefinitions` with `ManagedByFeature` propagated from the reserved template. When the toggle flips false, the system hard-removes them. The cascade rule (disabling Fuel Analysis also force-disables Throttle Consumption) is enforced at the toggle handler.
- **Custom channels are locked to `Distribution ∈ {CarLocal, CarToCloud}` and `Scope = PerCar`.** A user-defined channel in the per-car configuration is by definition car-originated and car-scoped. The Edit Channel UI's Distribution dropdown only offers the two car-side options when `kind = custom`; the Scope dropdown is hidden (effectively locked to `PerCar`). Reserved-channel selections inherit `Distribution`, `Scope`, and `ManagedByFeature` from the template; the corresponding dropdowns are disabled.
- **Defaults preserve today's behavior.** Existing (and future) reserved channels that don't set these fields default to `Distribution = CarToCloud`, `Scope = PerCar`, `ManagedByFeature = null`. No migration is needed for the 50+ existing reserved channels except the small set whose desired behavior differs from the default — `FuelConsumption` (repurposed to `CloudLocal` for the reconciler effectiveRate output) plus the new fuel-analysis and throttle-consumption channels added by ADR-0006.
- **The telemetry stream split.** The original `sendToCar` flag (documentation-only — never actually present in the runtime stream format) is gone. The runtime `car-channel-values` payload remains `ChannelValue[]` keyed by carKey (`SessionIndex`-keyed values) and is unchanged. A new `team-channel-values` stream carries `TeamChannelValue[]` payloads keyed by `team-{teamId}` for the `PerTeam` axis; values are addressed by stable `ChannelId` Guid rather than `SessionIndex` (which is meaningful only per car). See the **Implementation notes** below for the full stream/state/consumer-group inventory.

## Implementation notes (2026-05-24)

The body of this ADR was accepted on 2026-05-23 and captures the decision to make routing declarative on `ChannelDefinition`. The implementation that landed introduces a small set of concrete runtime artifacts worth recording here so that the ADR matches reality. Several of these resolve open points the original body had — they are explicitly *implementation choices*, not new ADRs.

**Stream model (two streams, not one with an extra field):**

| Stream | Field key | Payload type | Producers | Consumers |
| --- | --- | --- | --- | --- |
| `car-channel-values` | `team-{teamId}-car-{car}` (carKey) | `ChannelValue[]` (SessionIndex-keyed) | Car (`CarHub.SendChannelValuesAsync`); cloud `PerCar`/`CloudToCar` producers (e.g., WebApi for `ManualFuelAddedGallons`) | `TelemetryStreamConsumer` (state), `CarGatewayForwardingService` (forwards `CloudToCar` to the same car) |
| `team-channel-values` | `team-{teamId}` | `TeamChannelValue[]` (ChannelId-keyed) | Cloud `PerTeam` producers (e.g., WebApi for `RaceFlagState`) | `TeamChannelStreamConsumer` (state), `TeamChannelForwardingService` (fans out to every connected car on the team, re-indexing to per-car `SessionIndex`) |

**State storage:**
- `car-channels:{carKey}` — Redis Hash keyed by `SessionIndex` (string), value `ChannelValueSnapshot`. Change notifications on `car-channel-changes:{carKey}` with `ChannelChangeNotification`.
- `team-channels:{teamId}` — Redis Hash keyed by `ChannelId` Guid (string), value `ChannelValueSnapshot`. Change notifications on `team-channel-changes:{teamId}` with `TeamChannelChangeNotification`.

**Consumer groups:**
- `channelproc` — state-storage group on both streams (`TelemetryStreamConsumer`, `TeamChannelStreamConsumer`).
- `cargw` — forwarding group on both streams (`CarGatewayForwardingService`, `TeamChannelForwardingService`). Each stream tracks its own group state independently.

**Team membership index:**
- `team-connected-cars:{teamId}` — Redis SET of currently-connected carKeys in a team. Maintained by `CarHub` via `SADD` on the first `SendChannelValuesAsync` for a (connection, carKey) pair and `SREM` on disconnect. Used by `TeamChannelForwardingService` for O(1) fan-out lookup.
- `CarHubConnectionState.TeamId` (MessagePack key 4, additive — old serialized state deserializes with `TeamId = 0` and the disconnect handler skips the SREM in that case).

**Resolver:**
- `ICarChannelDefinitionResolver` in CarGateway. Single cache keyed by `configurationId` (Guid), value holds both the forward map (`SessionIndex → ChannelDefinition`) and the reverse map (`ChannelId → SessionIndex`). Invalidation is implicit: the car's `car-active-config:{carKey}` pointer flips to a new configurationId on next `SendChannelValuesAsync`, so the stale entry simply stops being reached. No event subscription.

**In-car receive surface:**
- `ICloudClient.ChannelValuesReceived` event on the car raises when the cloud invokes `ReceiveChannelValues` on the hub. Exception isolation around subscribers is built in so a faulty handler can't break the receive path. No in-car subscriber yet — wiring a received value into the in-car channel pipeline is deferred until a feature needs it (e.g., the throttle-calibration override path in ADR-0006).

## Amendment (2026-05-25): Partial distribution editability and cloud-origin custom channels

After a few weeks of using the original model, two of the **Consequences** above turned out to over-restrict the UX without a corresponding correctness benefit. The original decision body still records the 2026-05-23 reasoning; this amendment records what changed and why.

**What changed:**

1. **`Distribution` is now user-editable by default on all channels** (reserved and custom), within the channel's origin family. The original ADR locked distribution in two places:
   - The Consequence that said *"Custom channels are locked to `Distribution ∈ {CarLocal, CarToCloud}` and `Scope = PerCar`"*. The custom-channel lock to car-side distributions is **superseded** — origin is still chosen once at channel creation, but a custom channel created with cloud origin can pick either `CloudLocal` or `CloudToCar`. `Scope = PerCar` remains locked for custom channels.
   - The Consequence that said *"The Channels list renders rows with `ManagedByFeature != null` in a locked/read-only state ... Edit and delete actions are disabled for those rows"*. Edit is now **enabled** so the user can adjust `Distribution`; delete remains disabled (feature toggle still owns lifecycle).

2. **A new `bool IsDistributionLocked` field on `ChannelDefinition`** (default `false`) carries the lock signal for the small set of reserved channels whose distribution the feature genuinely requires:

   | Channel | Distribution | Why locked |
   | --- | --- | --- |
   | `ThrottleProxyFuelUsed` | `CarToCloud` | Cloud-side `ThrottleProxyIntegralEstimator` short-circuits without it; the in-car proxy exists to feed cloud reconciliation. |
   | `ThrottleProxyRate` | `CarToCloud` | Cloud maintains a running `∫ ThrottleProxyRate dt` totalizer; both `ThrottleProxyIntegralEstimator` and `ThrottleProxyGridEstimator` short-circuit without it. |
   | `ThrottleProxyConfidence` | `CarToCloud` | Threshold gate for both throttle-proxy estimators. |
   | `ThrottleProxyGridCoverage` | `CarToCloud` | Threshold gate for `ThrottleProxyGridEstimator`. |

   No other reserved channel is locked. Notably, `RaceFlagState` (originally `CloudToCar` with the intent that the car's dash could display it) is **not** locked — no in-car consumer exists today, and the cloud-side fuel reconciler reads `RaceFlagState` from the `team-channels:{teamId}` hash regardless of forwarding. A team that doesn't want the cloud→car uplink can flip it to `CloudLocal` with no functional impact.

3. **Custom channels can be created as cloud-origin.** At creation, the Edit Channel UI offers an Origin radio (`Car` / `Cloud`). The Distribution dropdown shows the two origin-matching options. Origin is fixed after creation. The producer for a cloud-origin custom channel reaches the runtime stream through the existing `ICarChannelPublisher` / `ITeamChannelPublisher` abstractions used by Alarms, the same path future cloud-side modules (math, counters, timers, tables, user conditions, custom modules) will use.

**Server-side enforcement.** `ConfigurationController.SaveCarConfiguration` validates each incoming `ChannelDefinition` against the prior persisted version and the reserved template:
- Rejects edits to a reserved channel whose template has `IsDistributionLocked = true` if the incoming `Distribution` differs from the template's value.
- Rejects edits where the incoming `Distribution`'s origin family (`{CarLocal, CarToCloud}` vs `{CloudLocal, CloudToCar}`) differs from the prior persisted value. This is the "origin is fixed post-create" guard for both custom and reserved channels.

**Migration.** None required. `IsDistributionLocked` is a new bool on the JSON-serialized `ChannelDefinition`; existing rows in `CarConfigurationTable.ConfigurationJson` deserialize with `false` (the editable default), and the four `ThrottleProxy*` reserved templates carry `true` at startup.

## References

Internal:
- [ADR-0001](0001-cargw-direct-config-write.md) — CarGateway's direct Postgres write path; same component that gains the definition cache here.
- [ADR-0002](0002-channelprocessor-redis-evaluator-state.md) — the "any replica processes any car" invariant for ChannelProcessor; this ADR's `Distribution` field is read by ChannelProcessor without breaking that invariant (the cache lookup is read-only and identical across replicas).
- [ADR-0006](0006-throttle-proxy-computed-in-car.md) — engineer override path for the throttle-proxy calibration uses a `CloudToCar` reserved channel rather than a per-message `sendToCar` flag.
- [design.md — CarGateway](../../design.md#cargateway) and [Shared infrastructure](../../design.md#shared-infrastructure) — system-level descriptions updated to reflect this ADR.
- [CONTEXT.md — Telemetry Stream](../../CONTEXT.md#telemetry-stream) — glossary entries for `Channel Distribution`, `Channel Scope`, and `Feature-Managed Channel`.
