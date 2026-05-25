# Implementation Plan
List of tasks to be implemented

# Configuration UI
[ ] Navigation tree
[ ] Validation service / Error List
[ ] General settting
[ ] Channel configuration
[ ] CAN Bus configuration
[ ] Table configuration
[ ] Timer Configuration
[ ] Logging configuration
[ ] Math configuration
[ ] User condition configuration
[ ] Alarm configuration
[ ] Counter configuration

# Local Race Management On Prem API Service
## Configuration Support
[ ] Load summray list of saved car configurations
[ ] Load specific full car configuration
[ ] Save car configuration

## Car 
[ ] Connect to car - get status and installation versions, etc
[ ] Upload new configuration version
[ ] Car Firmware update

## Cloud
[ ] Sync local configuration changes to cloud
[ ] Get new configuration from cloud
[ ] Sync reserved channels

# Cloud UI
[ ] Base Site setup - menus, login, API clients, etc.
[ ] Telemetry Dashboard

# Cloud Services
[ ] Car Telemetry processor
[ ] Channel Processor

## Alarm Processor (ChannelProcessor / Alarms)
Cloud-side alarm evaluation as a BackgroundService inside ChannelProcessor. Reuses
`Channels.Alarms.AlarmEvaluation` with cloud-shaped repositories and persists active
alarms + an event history for the Race Monitor UI.

### Schema (new EF migration)
[ ] `AlarmDefinitions` table — `(Id, TeamId, CarId?, Name, Message, DisplayChannelSourceColorHex, TimeAfterAckToDisplaySecs, AlarmStatusChannelId?, StatementJson jsonb)`, index `(TeamId, CarId)`. CarId null = team-level (applies to every car on the team); set = car-only. Team and car definitions both fire when conditions trip — no suppression link.
[ ] `ActiveAlarms` table — composite PK `(TeamId, CarId, AlarmDefinitionId)`, `IsActive`, `IsAcknowledged`, `LastActivatedAt?`, `LastAcknowledgedTimestamp?`.
[ ] `AlarmEvents` append-only table — `(Id, TeamId, CarId, AlarmDefinitionId, EventType, Timestamp)`, EventType ∈ {Activated, Deactivated, Acknowledged}.

### Per-message worker
[ ] `AlarmProcessorWorker` BackgroundService — own Redis consumer group `channelproc-alarm` on `car-channel-values` stream. For each entry: parse carKey, resolve CarId, load effective alarm set, build car-scoped repositories, invoke `AlarmEvaluation.UpdateAlarmsAsync()`, diff prior vs post `IsActive` and emit `ActiveAlarms` upsert + `AlarmEvents` rows, then ACK.

### Channel scope dispatch
[ ] `CarScopedChannelRepository` — `IChannelRepository` impl that dispatches reads by `ChannelDefinition.Scope`: PerCar → `car-channels:{carKey}` hash; PerTeam → `team-channels:{teamId}` hash. `SetChannelValueAsync` (used by `AlarmStatusChannelId` writes) publishes to the telemetry stream via `ICarChannelPublisher`/`ITeamChannelPublisher` with `source = "cloud:alarm"`, distribution from the status channel's `ChannelDefinition`.
[ ] `CarScopedChannelDefinitionRepository` — wraps `ICarChannelDefinitionResolver` so `AlarmEvaluation` can read channel metadata.
[ ] Extend `ICarChannelDefinitionResolver` with `GetChannelDefinitionAsync(carKey, channelId Guid)` if not derivable from existing maps.

### Evaluator state in Redis (ADR-0002)
[ ] `RedisAlarmRepository` (`IAlarmRepository`) — keyed by `(carKey, alarmId)`.
[ ] `RedisStatementStateRepository` — keyed by `(carKey, statementId)`.
[ ] `RedisComparisonDurationRepository` — keyed by `(carKey, comparisonId)`.
[ ] `RedisPreviousChannelValueRepository` — keyed by `(carKey, channelId)`.

### Persistence
[ ] `ActiveAlarmStore` — upsert `ActiveAlarms` row + append `AlarmEvents` on edge transitions; preserves ACK ordering rule.

### Config repository
[ ] `IAlarmDefinitionRepository` — `GetForCarAsync(teamId, carId)` (returns team-level ∪ car-level), `SaveAsync(...)`. EF + HybridCache-backed; cache invalidation on `CarConfigChanged` pub/sub. `SaveAsync` is implemented but not yet called — the WebApi follow-up will wire it.

### Wiring
[ ] Add `Consts.CHANNEL_PROC_ALARM_CONSUMER_GROUP = "channelproc-alarm"`.
[ ] Register all alarm services and the worker in `ChannelProcessor.Program.Main`.

### Tests (`Cloud.Tests/ChannelProcessor/Alarms/`)
[ ] `CarScopedChannelRepositoryTests` — PerCar vs PerTeam dispatch, output publish routing.
[ ] `RedisAlarmRepositoryTests` and sibling state repository tests — round-trip + key conventions.
[ ] `ActiveAlarmStoreTests` — upsert semantics + edge-transition event emission.
[ ] `AlarmProcessorWorkerTests` — given seeded definitions and a stream message, assert `ActiveAlarms` + `AlarmEvents` writes.

## Alarm Processor — WebApi follow-up (next slice)
Adds CRUD for `AlarmDefinitions`, the Race Monitor §3 active-alarms feed, the acknowledge
action, and live SignalR push of alarm-state changes. Lands in the existing
`ConfigurationController` (`#region Alarms`) and extends the existing
`ChannelPropagatorService` — no new controller or worker files.

### Shared
[ ] `Cloud.Shared/Alarms/RedisAlarmStateGateway.cs` — `GetAsync` / `SetAsync` / `AcknowledgeAsync` over the same Redis key + JSON shape used by `RedisAlarmRepository`. Shared by WebApi (ack endpoint) and ChannelProcessor (existing repo refactored to delegate Get/Set here, keeping its tick-recording responsibility on top).
[ ] `Cloud.Shared/Alarms/AlarmChangeNotification.cs` — payload travelling over both Redis pub/sub and SignalR: `TeamId`, `CarNumber`, `AlarmDefinitionId`, `EventType`, `IsActive`, `IsAcknowledged`, `Timestamp`.
[ ] `Consts.ALARM_CHANGES_CHANNEL = "alarm-changes:{0}"` (keyed by teamId) and `Consts.ALARM_CONFIG_CHANGED_CHANNEL = "alarm-config-changed:{0}"` (keyed by teamId).

### ChannelProcessor adjustments
[ ] Refactor `RedisAlarmRepository` to call `RedisAlarmStateGateway` for Redis Get/Set; keep the `TickRecord` capture path on top.
[ ] `ActiveAlarmStore.RecordTickAsync` — after `db.SaveChangesAsync`, publish `alarm-changes:{teamId}` for each edge transition (Activated, Deactivated). Same ACK-ordering rule (publish before stream ACK).
[ ] `AlarmDefinitionRepository` — subscribe to `alarm-config-changed:{teamId}` Redis pub/sub and evict the cache entries for the affected `(teamId, carNumber)` immediately, replacing the 2-min TTL staleness window.

### ConfigurationController `#region Alarms`
All endpoints are kebab-case under `/v1/configuration/` matching the existing convention. Reads require team membership; writes require the `admin` role; **ack requires only team membership** (race engineers and crew need to ack during a live race).

#### Definitions CRUD
[ ] `LoadAlarmDefinitionsAsync(int teamId, string? carNumber)` GET — returns team-level ∪ optional car-level `AlarmDefinitionDto[]`.
[ ] `LoadAlarmDefinitionAsync(int teamId, Guid alarmId)` GET — single definition.
[ ] `SaveAlarmDefinitionAsync(int teamId, [FromBody] AlarmDefinitionDto)` POST — upsert. `Id == Guid.Empty` triggers insert; otherwise updates the matching row. Reject scope changes on update (delete + create instead). On success, publish `alarm-config-changed:{teamId}` so ChannelProcessor evicts its cache.
[ ] `DeleteAlarmDefinitionAsync(int teamId, Guid alarmId)` DELETE — hard delete (FK cascade clears `ActiveAlarms` and `AlarmEvents`). Matches Races, not Cars.

**Validation depth (shape only)**: non-empty `Name`, valid hex color, `StatementJson` deserializes as `StatementDefinition` with at least one `ActivateComparison` row containing at least one comparison, every comparison has non-empty `Id` and `ChannelId` Guids. **Do not cross-reference channel ids against any car config** — definitions can be saved ahead of channel sync.

#### Active alarms feed
[ ] `LoadActiveAlarmsAsync(int teamId, bool includeAcknowledged = false)` GET — returns `ActiveAlarmDto[]` joining `ActiveAlarms` ⇈ `AlarmDefinitions` (so `Name`, `Message`, `DisplayChannelSourceColorHex`, `TimeAfterAckToDisplaySecs` come back in one round-trip). Default filter: `IsActive = true AND (includeAcknowledged OR NOT IsAcknowledged)`. Sort: `LastActivatedAt DESC`.
[ ] `AcknowledgeAlarmAsync(int teamId, string carNumber, Guid alarmId)` POST — sequence (ADR-0002 ordering, all writes before response):
  1. Validate membership and row existence.
  2. `RedisAlarmStateGateway.AcknowledgeAsync` (read-modify-write).
  3. Update `ActiveAlarms` row (`IsAcknowledged=true`, `LastAcknowledgedTimestamp=now`).
  4. Append `AlarmEventRow { EventType = Acknowledged, Timestamp = now }`.
  5. Publish `alarm-changes:{teamId}` with the `Acknowledged` notification so other browsers see it.
  6. Return updated `ActiveAlarmDto`.

  Idempotent: ack on an already-acked alarm is a no-op (200, same state, no duplicate event row).

#### DTOs
[ ] `AlarmDefinitionDto` — flattens `AlarmDefinitionRow`; `Statement` is a strongly-typed `StatementDefinition` (not raw JSON string) for the wire.
[ ] `ActiveAlarmDto` — joined view per above.

### SignalR push — extend `ChannelPropagatorService` (no new BackgroundService)
[ ] Add a second subscription `RedisChannel.Pattern("alarm-changes:*")` alongside the existing channel-changes pattern.
[ ] On message: parse `teamId` from the channel name, deserialize the `AlarmChangeNotification`, fan out to `WebHub.TeamGroup(teamId)` via a new `IWebHubClient.AlarmChanged(AlarmChangeNotification)` method.
[ ] Extend the existing periodic snapshot loop (2.5s) to also broadcast `IWebHubClient.AlarmSnapshot(ActiveAlarmDto[])` per connected team — catches disconnected/late-joining browsers up to current state.
[ ] `WebHub.SubscribeToTeam` sends an initial `AlarmSnapshot` immediately after the existing `ChannelSnapshot` so the Race Monitor §3 panel populates without waiting for the periodic tick.

`IWebHubClient` additions:
[ ] `Task AlarmChanged(AlarmChangeNotification change)`
[ ] `Task AlarmSnapshot(ActiveAlarmDto[] alarms)`

**Multi-replica caveat** (inherited from ChannelPropagatorService, not introduced here): every replica forwards each change, producing duplicates. Same constraint as today's channel changes — fix in the existing leader-election work.

### TypeScript codegen — `WebApi/TypeScript/ModelGenerationSpec.cs`
[ ] Add to `InterfaceTypes`: `AlarmDefinitionDto`, `ActiveAlarmDto`, `AlarmChangeNotification`, `AlarmEventType`, `StatementDefinition`, `ComparisonDefinition`, `LogicType`.
[ ] Add `AlarmChangeNotification` to `MessagePackTypes` (WebHub uses MessagePack protocol).

### Tests (`Cloud.Tests/`)
[ ] `WebApi/Controllers/AlarmsTests.cs` — CRUD round-trip; validation rejects (empty ActivateComparisons, bad color, scope change on update); member-vs-admin auth; ack updates Redis + ActiveAlarms + AlarmEvents + publishes; idempotent ack.
[ ] `Cloud.Shared/Alarms/RedisAlarmStateGatewayTests.cs` — JSON round-trip; `AcknowledgeAsync` sets both ack fields.
[ ] `WebApi/Telemetry/ChannelPropagatorServiceTests.cs` (extend existing) — alarm-changes pattern routes to `AlarmChanged`; snapshot loop also calls `AlarmSnapshot`.

### Implementation slice order
1. `RedisAlarmStateGateway` + `AlarmChangeNotification` + Consts.
2. Refactor `RedisAlarmRepository` onto the gateway; add pub/sub publish in `ActiveAlarmStore`; add the cache-invalidation listener in `AlarmDefinitionRepository`.
3. Controller endpoints + DTOs + validation.
4. `IWebHubClient` additions + `WebHub.SubscribeToTeam` initial snapshot.
5. Extend `ChannelPropagatorService` with the alarm subscription + periodic alarm snapshot.
6. TypeScript codegen update.
7. Tests.