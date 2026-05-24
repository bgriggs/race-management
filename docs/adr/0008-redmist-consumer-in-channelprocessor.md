# ADR-0008: RedMist consumer lives in ChannelProcessor with a Redis-leased subscription

**Status:** Accepted
**Date:** 2026-05-24

## Context

The system integrates with [RedMist.racing](https://api.redmist.racing) — an external timing-and-scoring backend the team consumes for race position, flag state, lap data, and pit detection. RedMist exposes two relevant surfaces:

- A REST API (`https://api.redmist.racing/status/v2/Events/...`) for event discovery, session metadata, lap history, and current session state.
- A SignalR hub (`https://api.redmist.racing/status/event-status`, `StatusHub`) that streams `CarPositionPatch[]` and `SessionStatePatch` updates while an event is live.

Both surfaces authenticate via the OAuth 2.0 Client Credentials flow against RedMist's Keycloak realm (`auth.redmist.racing`, realm `redmist`) — separate from the race-management Keycloak. Each team holds one `client_id` / `client_secret` pair, already stored in [`SiteSettings`](../../services/cloud/Cloud.Shared/Database/Models/SiteSettings.cs) (`RedMistClientId`, `RedMistClientSecret`) and editable through [`ConfigurationController`](../../services/cloud/WebApi/Controllers/ConfigurationController.cs).

The integration needs to:

1. Hold a long-lived SignalR subscription to RedMist's `StatusHub` for the team's currently-active event.
2. Translate `CarPositionPatch` updates into reserved-channel publishes on the existing telemetry streams (`InPit`, `Position`, `ClassPosition`, `RaceFlagState`, plus a derived `CurrentStintMinutes` and `StintCount`) so that fuel analysis, alarms, dashboards, and `ChannelLogs` all consume them uniformly.
3. Expose REST endpoints on WebApi for picking the RedMist event during Race setup and for an on-demand competitor analysis path that proxies through to RedMist's `LoadCarLaps` / `LoadSessionLaps` (no local persistence of competitor data).

The architectural question this ADR resolves: **where does the hub-holding component live, how is it coordinated across replicas, and how does it decide which event to subscribe to?**

The discussion considered three placements for the hub-holding component:

- **Option A: A new dedicated service `RedMistGateway`.** Mirrors the existing `CarGateway` pattern (a gateway service whose only job is to hold long-lived inbound connections from an external source and produce stream messages). One Kubernetes deployment, one set of secrets, one independent scale axis. Cost: a fourth service to deploy and operate for what is realistically one long-lived connection per team.
- **Option B: Fold into `ChannelProcessor` as a new hosted worker (`RedmistConsumer`).** ChannelProcessor is already a stateless background worker, already publishes derived values to the team and per-car streams (the Fuel Reconciler is the canonical example), and already runs multiple replicas with Redis-backed coordination per [ADR-0002](0002-channelprocessor-redis-evaluator-state.md). The RedMist data flow is conceptually "another producer of channel values."
- **Option C: Fold into `WebApi`.** WebApi already proxies RedMist REST calls for the event picker and connection-status endpoint. Adding the hub-holder co-locates the integration. Cost: WebApi's role becomes mixed — inbound request/response + outbound browser SignalR + outbound long-lived inbound-from-external SignalR; multi-replica leadership election still needed, plus WebApi pods become heavier.

A secondary axis runs through all three: **which replica holds the connection.** Both B and C run multiple replicas; both need exactly one replica per team subscribed to RedMist at any time, or `RaceFlagState` and the per-car publishes get duplicated. A and C share C's leadership-election problem (A would face it only if scaled past one replica). Several coordination mechanisms were on the table:

- **A1: Redis SETNX lease** keyed by `redmist:lease:{teamId}`, claimed on a rolling 30s tick with a 60s expiry; whichever replica wins renews while it holds the connection. Bounded failover (≤60s gap). No new infrastructure.
- **A2: Consistent hash on `(teamId, eventId)`** with replica membership tracked via Redis pub/sub heartbeats. Re-balances automatically; needs a membership protocol that handles split-brain.
- **A3: StatefulSet with `replicas: 1`.** Kubernetes guarantees a single pod; trivially correct; the single pod is a SPOF and is effectively "the separate service from Option A, just shaped as a singleton."
- **A4: Distributed-lock library** (`DistributedLock.Redis` or similar). Same idea as A1, packaged.

A third question: **which event does the consumer subscribe to?** A team may have multiple `Race` rows with `RedMistEventId` set — past, present, future. Options:

- **D1: Time-window only.** Subscribe iff `now ∈ [Race.Start − 30min, Race.Start + Duration + 30min]`. Simple; doesn't handle red-flag delays that push the race past its scheduled end.
- **D2: RedMist `IsLive` poll only.** Subscribe iff one of the team's paired event IDs is currently `IsLive` in RedMist's API. Real-world-accurate end; cannot subscribe pre-race for engineer setup.
- **D3: Manual `Race.IsActive` flag.** Engineer-armed; max control; max forgettability.
- **D4: Time-window with RedMist-`IsLive` extension.** Time-window for the start side; window auto-extends as long as RedMist still reports the event live. Combines deterministic activation with real-world-accurate deactivation.

A fourth: **how is a `CarPositionPatch.Number` (string) mapped to a local car?** The team-car entity is [`Car`](../../services/cloud/Cloud.Shared/Database/Models/Car.cs) keyed `(TeamId, Number)`, and the active configuration lives in [`CarConfigurationTable`](../../services/cloud/Cloud.Shared/Database/Models/CarConfigurationTable.cs) keyed `(TeamId, Car)`. Car numbers in club racing change race-to-race, but the team's *current* `CarConfiguration.Car` value reflects the current race's number. No separate mapping table or per-race override is needed if the team keeps its `CarConfiguration.Car` accurate.

A fifth: **the Race / Event / Session model.** RedMist events contain multiple sessions (practice, qualifying, race). The fuel-analysis model in `design.md` is per-session. The choices were (i) introduce a `RaceSession` table populated from RedMist's `LoadSessions`, (ii) make a local `Race` row equal a single RedMist session, or (iii) keep `Race` = RedMist event and have the consumer follow whichever session is currently active under it.

A sixth: **where does competitor data live?** Options: write all field-car patches into a `CompetitorLapLog` table for post-race analysis; query RedMist on demand whenever the UI needs competitor data; or both.

## Decision

**The RedMist integration is implemented as a `RedmistConsumer` hosted worker inside the existing `ChannelProcessor` service (Option B), coordinated by a Redis SETNX lease keyed per team (A1), activated by a time-window + RedMist-`IsLive`-extension rule (D4), mapping car numbers via the team's active `CarConfiguration.Car` value, treating the local `Race` row as a pairing to a RedMist event with the active session followed in real time, and exposing competitor data on demand through a WebApi proxy with no local persistence.**

Concretely:

### Placement and lifecycle

- A new `RedmistConsumer` background worker is added to ChannelProcessor alongside `CloudChannelEvaluator`, `AlarmEvaluator`, and `ChannelLogger`. It runs on every replica.
- Every 30 seconds, each replica evaluates whether any team it knows about has a Race that should be subscribed to (see *Activation rule* below). For each qualifying team, the replica attempts `SET redmist:lease:{teamId} {podId} NX EX 60`. The lease key is keyed by `teamId` only — the design assumes a team runs at most one series at a time and therefore one event subscription at a time.
- A replica that wins the lease for a team renews it every 30s (`SET ... XX EX 60`). If renewal fails or the replica is shutting down, it tears down its RedMist hub connection cleanly.
- Lease loss while subscribed triggers an immediate connection close and a publish of `redmist_connection_status = "disconnected"` to Redis (`redmist:status:{teamId}`); another replica will pick up the lease within 60 seconds.
- Multi-replica failover is bounded by the `EX 60` window. Duplicate publishes during the window are tolerable because `RaceFlagState`, `InPit`, `Position`, and `ClassPosition` are last-writer-wins idempotent at the channel level.

### Activation rule (which event, when)

For each team the replica knows about, the rule evaluated every 30s is:

> Subscribe to the `Race` row `R` for team `T` where:
> - `R.RedMistEventId IS NOT NULL`,
> - `now ≥ R.Start − 30min`, AND
> - either `now ≤ R.Start + R.Duration + 30min`, **or** the RedMist event is currently `IsLive` (cached `LoadLiveEvents` result, 60s TTL per team).
>
> If multiple Race rows qualify, pick the one with the smallest `|R.Start − now|`. Tie-break by `R.Id` ascending.
>
> When no Race qualifies, the replica does not attempt the lease (and the holder, if any, releases on detection).

Lease churn from a single team across replicas during the activation transition is acceptable — `SETNX` is cheap and the worst case is a few seconds of duplicate connections that drop themselves on the next renewal cycle.

### Race ↔ Event ↔ Session model

- The local `Race` row pairs to a RedMist *event*, via the existing nullable `RedMistEventId` and `RedMistOrganizationId` columns. No schema changes.
- There is no local `RaceSession` table. The "current session" is whichever RedMist session is reported live under the paired event, read from `GetCurrentSessionState` at connect time and tracked from inbound `SessionStatePatch` thereafter.
- When the active RedMist session changes (practice → qualifying → race), the consumer:
  1. Closes any open fuel-analysis state (`FuelWindows`, `Stints`, `RefuelEvents`) marking them `ClosedBySessionEnd`.
  2. Resets the Redis state used by the Fuel Reconciler for that team's cars.
  3. Re-emits `RaceFlagState = Green` (the default-when-absent — see [design.md](../../design.md#flag-condition-adjustment)).
  4. Begins fresh state for the new session.
- Per-session fuel-analysis rows denormalize the RedMist `SessionId` (int) onto each row so post-race queries can filter by session without a join.

### Subscription model and initial sync

- On lease acquisition, the consumer:
  1. Acquires a token from `auth.redmist.racing/realms/redmist` via Client Credentials using the team's `SiteSettings.RedMistClientId` / `RedMistClientSecret`.
  2. Calls `GET /status/v2/Events/GetCurrentSessionState?eventId={id}` once for a full snapshot.
  3. Translates the snapshot into channel-value publishes (see *Channel publishing* below).
  4. Opens the `StatusHub` SignalR connection (`SkipNegotiation = true`, `WebSockets` only, infinite-retry reconnect policy).
  5. Calls `SubscribeToEventV2(eventId, null)` on the hub.
- On hub reconnect (network blip, etc.), the consumer re-invokes `GetCurrentSessionState` and re-publishes all current values. `LoadCarLaps` is not used in the main path; it is reserved for the future competitor-analysis WebApi proxy or for explicit gap-recovery if needed.
- On `ReceiveReset` from the hub, treat it as a reconnect: re-call `GetCurrentSessionState` and re-emit.

### Channel publishing

The consumer publishes to the existing per-team and per-car channel streams using the existing `ICarChannelPublisher` and `ITeamChannelPublisher` infrastructure. No new streams are introduced.

Sources and scopes:

| Channel | Source | Scope | Distribution |
| --- | --- | --- | --- |
| `RaceFlagState` | RedMist (`SessionStatePatch.Flag`) | PerTeam | `CloudLocal` (was `CloudToCar` for forwarding to the car's dash — kept as `CloudToCar`; see *Consequences*) |
| `Position` | RedMist (`CarPositionPatch.OverallPosition`) | PerCar | `CloudLocal` (newly explicit; previously unset) |
| `ClassPosition` | RedMist (`CarPositionPatch.ClassPosition`) | PerCar | `CloudLocal` |
| `InPit` | RedMist (`CarPositionPatch.IsInPit`) | PerCar | `CloudLocal` |
| `CurrentStintMinutes` | Derived in `RedmistConsumer` (from `InPit` transitions, seeded by initial sync) | PerCar | `CloudLocal` |
| `StintCount` | Derived in `RedmistConsumer` (increments on each pit-in) | PerCar | `CloudLocal` |

Channels that remain car-sourced (no change): `LastLapTime`, `BestLapTime`, `SessionTime`, `Latitude`, `Longitude`. The RedMist hub also reports lap times and lat/lon for every car in the field, but for *team-owned cars* the in-car CAN-bus values are authoritative; the RedMist copy of these is ignored to avoid two-producer drift.

The consumer iterates each `CarPositionPatch` and:

1. Looks up `CarConfiguration WHERE TeamId = leasedTeamId AND Car = patch.Number`.
2. **Match**: publishes the per-car channels above via `ICarChannelPublisher.PublishAsync(teamId, patch.Number, …)`. Values flow through the existing change-filter pipeline; only changed values reach `ChannelLogs`.
3. **No match**: drops the patch silently (it is a competitor car; competitor data is on-demand only — see below).

`RaceFlagState` is published via `ITeamChannelPublisher.PublishAsync(teamId, …)` once per `SessionStatePatch` flag change, independent of any car mapping.

Stint state (`CurrentStintMinutes`, `StintCount`) is rebuilt from `GetCurrentSessionState` on every connect/reconnect — the consumer is stateless across reconnects and does not persist its own derived state to Redis. The initial-sync `GetCurrentSessionState` gives current `IsInPit` per car; stint start time is estimated as `session.SessionStartTime` (an upper bound that gets corrected to a tighter value at the next pit cycle). The consumer emits `CurrentStintMinutes` once per 60 seconds while on track and on every `InPit` transition; emits `0` while `InPit = true`.

On consumer detach (lease loss, Race deactivation, session change), the consumer publishes `RaceFlagState = Green` once before tearing down so downstream consumers don't latch onto a stale yellow.

### WebApi endpoints

WebApi adds (under `/v1/redmist/`):

| Endpoint | Purpose |
| --- | --- |
| `GET /v1/redmist/events/live-and-upcoming` | Proxies RedMist `LoadEvents(startDateUtc = userNow − 7 days)` with the calling team's stored client credentials. Result is cached per-team in WebApi for 60 seconds. Returns 4xx when team credentials are missing or RedMist authentication fails — no anonymous fallback. |
| `GET /v1/redmist/events/{eventId}` | Proxies RedMist `LoadEvent(eventId)`. Used by the Race edit form to show event metadata (track, sessions, start time) after the engineer picks a pairing. |
| `GET /v1/redmist/connection-status` | Returns the consumer's connection state for the calling team: `connected`, `reconnecting`, `auth-failed`, `no-event-paired`, `disconnected`, plus last-change timestamp. Read from Redis key `redmist:status:{teamId}` written by `RedmistConsumer`. |
| `GET /v1/redmist/competitors/laps?eventId=…&sessionId=…&carNumber=…` | (Deferred to competitor-analysis feature, scope reserved.) Proxies RedMist `LoadCarLaps` with team credentials. No local storage. |

All endpoints require an authenticated race-management user (browser session); team credentials never leave the server.

### Competitor data

Per-car field data for non-team cars is **not** persisted locally. The future Competitor Analysis section of the Race Monitor renders via the on-demand WebApi proxy listed above. Trade-off: each competitor-render hits RedMist; cached at WebApi where reasonable. Acceptable for v1 — competitor data is read sparingly compared to team-car data, and RedMist's REST surface already serves the live-and-archived-event laps.

### Error handling and failure modes

- **Missing credentials at activation**: log + raise alert + skip the lease attempt; the `redmist_connection_status` Redis key reads `auth-failed`; the UI surfaces it via `/v1/redmist/connection-status`.
- **Authentication failure mid-race**: log + release the lease + write `redmist_connection_status = "auth-failed"`. Other replicas attempt the lease and hit the same failure → same status; the engineer sees the alert and updates credentials.
- **Hub disconnect**: handled by the SignalR `WithAutomaticReconnect(InfiniteRetryPolicy)` policy already used by the RedMist sample client. On reconnect, run the initial-sync path.
- **RedMist API 5xx during initial sync**: retry with exponential backoff up to 5 minutes; meanwhile `redmist_connection_status = "reconnecting"`.

## Rationale

- **ChannelProcessor is the natural home.** The RedmistConsumer's primary output is reserved-channel values on the same per-team and per-car streams that the Fuel Reconciler already publishes to. The existing infrastructure (channel publishers, change-filter pipeline, `IStateRepository` patterns, Redis backplane, ACK ordering rule from [ADR-0002](0002-channelprocessor-redis-evaluator-state.md)) covers everything the consumer needs. A separate `RedMistGateway` service would duplicate that infrastructure for one connection per team.
- **The Redis SETNX lease is the minimum-viable coordination.** With at most one event subscription per team, a per-team key is correct. Failover is bounded by the lease expiry. There is no new infrastructure dependency — Redis is already on the critical path for ChannelProcessor. Consistent-hash or distributed-lock libraries would add complexity without buying anything at this scale.
- **The time-window-with-`IsLive`-extension activation rule** combines a deterministic activation moment (the engineer doesn't have to remember to "arm" a Race) with real-world-accurate deactivation (a red-flag delay doesn't kick the consumer off mid-race). The 30-minute pre-window covers normal pre-race setup; the `IsLive` extension is the safety net for delays.
- **Treating `Race` as the event-level pairing and following the active session in real time** matches the engineer's mental model — "I'm at a race weekend; right now there's a session running; I want the system to track it" — without introducing a session-picker UI or a `RaceSession` schema. Denormalizing `RedMistSessionId` onto fuel-analysis rows preserves the post-race query capability that a session FK would have provided.
- **Mapping via the team's active `CarConfiguration.Car`** uses the value the engineer already maintains for every race. Car numbers change race-to-race in club racing; the engineer updates the configuration anyway. A separate mapping table or `RaceCarMapping` join would duplicate state that already exists.
- **Dropping non-team-car patches and reserving competitor data for on-demand WebApi proxy** trades a small per-render RedMist call cost for the elimination of a new persistence path. Competitor data is read sparingly compared to team-car data; the volume justifies the simpler architecture.
- **Connection status as a WebApi endpoint, not a reserved channel.** Connection state is on-demand, single-valued, and not a time-series the engineer wants to plot or alarm on. A channel would require all downstream consumers to learn about it for no benefit.

## Consequences

- **ChannelProcessor pods gain outbound dependencies on `auth.redmist.racing` and `api.redmist.racing`.** Network policy / egress rules in Kubernetes need to allow these hosts. The pods also become slightly stateful in lifecycle — losing a pod tears down its held leases (and therefore its hub subscriptions), and another replica picks them up within 60 seconds. The "any replica processes any car" invariant from [ADR-0002](0002-channelprocessor-redis-evaluator-state.md) still holds for the message-processing workers; the lease is an additional, orthogonal piece of replica-local state.
- **`RaceFlagState` keeps `Distribution = CloudToCar`** so the car's dash can display the flag. The other RedMist-sourced channels (`Position`, `ClassPosition`, `InPit`) are set to `Distribution = CloudLocal` explicitly — they are not forwarded down to the car. `ReservedChannels.cs` needs the explicit `Distribution = CloudLocal` annotation on those three channels (previously unset, defaulting to `CarToCloud` which was incorrect for cloud-produced values).
- **New reserved channels `CurrentStintMinutes` and `StintCount`** are added to `ReservedChannels.cs` with `Distribution = CloudLocal`, `Scope = PerCar`, `ManagedByFeature = "fuel-analysis"` (so they auto-inject when the team enables fuel analysis on a car configuration).
- **The Refuel Event detector's pit-lap anchor** ([RefuelEventDetector.cs:11-12](../../services/cloud/ChannelProcessor/FuelAnalysis/Refuel/RefuelEventDetector.cs#L11-L12) — currently deferred) is now implementable. It derives a pit-lap edge from `InPit` transitions plus lap progression (`LastLapTime` updates from the car) and applies the `≥20 minutes since last Refuel Event` guard from design.md.
- **WebApi adds the `/v1/redmist/...` endpoint family**, including the `connection-status` read that the Race Monitor uses to render a connection state pill.
- **Multi-tenancy and lease coordination interact safely.** The lease key includes `teamId`; teams are isolated. A given replica can hold leases for many teams simultaneously without ordering coupling between them.
- **Documentation updates.** [context.md](../../context.md) glossary: the `Race Event` / `Race Session` entries are replaced by a single `Race` entry reflecting the event-pairing-with-live-session-follow model; the "Race Event / Session Metadata" section is rewritten. [design.md](../../design.md) "Cloud Service Topology > ChannelProcessor" lists `RedmistConsumer` as the fourth hosted worker; "Reserved Channels Added" gains `CurrentStintMinutes` and `StintCount`.
- **No schema migration required for v1.** `Race.RedMistEventId` / `RedMistOrganizationId` and `SiteSettings.RedMistClientId` / `RedMistClientSecret` already exist. The only `ReservedChannels.cs` edits are explicit `Distribution = CloudLocal` annotations and the two new derived channels.

## Alternatives considered

- **Dedicated `RedMistGateway` service (Option A).** Cleanly bounded; matches the existing topology vocabulary. Rejected for now: pays a deployment cost (new k8s manifests, new secrets surface, new monitoring) for what is realistically one long-lived connection per team. Revisit if RedMist subscriptions need to scale independently of channel processing, or if the consumer's failure profile diverges from ChannelProcessor's.
- **Fold into WebApi (Option C).** Co-locates with the REST proxy. Rejected: WebApi's role becomes mixed (browser fan-out + outbound-from-external hub client), and multi-replica leadership election is still required, so it offers no simplification over B.
- **Consistent-hash leadership (A2).** Elegant but needs a membership protocol; over-engineered for one-event-per-team.
- **StatefulSet `replicas: 1` (A3).** Trivially correct; introduces a SPOF and is functionally equivalent to Option A, costing the same operational surface without the architectural clarity.
- **`RaceSession` table populated from RedMist `LoadSessions` (the session-as-entity model).** Cleaner glossary match but adds a schema entity for runtime data that RedMist already authoritatively serves. The denormalized `RedMistSessionId` on fuel-analysis rows preserves what queries would need anyway.
- **Local `CompetitorLapLog` for the future competitor-analysis feature.** Provides post-race query and historical analysis without re-fetching RedMist. Rejected: competitor data is read sparingly; the on-demand path is simpler and avoids a 10× volume disparity with team-car telemetry in a parallel pipeline.
- **Browser direct connection to RedMist's hub for the Race Position view.** Out of scope here — that view is rendered as an iframe of RedMist's own UI, not a "modified port" as earlier design notes had suggested.

## References

Internal:
- [ADR-0002](0002-channelprocessor-redis-evaluator-state.md) — ChannelProcessor's "any replica processes any car" invariant; the RedMist lease is an orthogonal addition that does not violate it.
- [ADR-0007](0007-declarative-channel-routing.md) — the `Distribution` / `Scope` / `ManagedByFeature` model that the new RedMist-sourced reserved channels conform to.
- [design.md — Cloud Service Topology](../../design.md#cloud-services) — service inventory updated to list `RedmistConsumer` under ChannelProcessor.
- [design.md — Reserved Channels Added](../../design.md#reserved-channels-added) — `CurrentStintMinutes` and `StintCount` added.
- [context.md — Race](../../context.md#race) — glossary entry replacing `Race Event` and `Race Session`.

External:
- [RedMist API documentation](https://docs.redmist.racing/)
- [RedMist Status API Swagger](https://api.redmist.racing/status/swagger/index.html)
- [RedMist TeamSample reference client](https://github.com/bgriggs/redmist-timing-scoring-backend/tree/main/samples/RedMist.TeamSample) — pattern for the SignalR + REST integration.
