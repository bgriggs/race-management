# Race Management — Domain Context

## Multi-Tenancy

The application is **multi-tenant**. Each tenant is a **Team** (a racing organization). All team-scoped entities — Cars, ChannelDefinitions, AlarmRules, Dashboards, CarConfigurations, ChannelLogs, RaceEvents, RaceSessions — carry a `TeamId` FK. All API endpoints and queries are scoped to the authenticated user's team. Teams are isolated from each other; no cross-team data access.

Keycloak users are associated with a team via a custom claim. Initially one team; the data model supports multiple from day one.

---

## Glossary

### Car
A physical race vehicle managed by the team. Each car has a unique Keycloak client identity used for cloud authentication. A car runs two systemd services: the **Core App** and the **Update Agent**.

### Channel
A discrete piece of data — e.g., sensor temperature, button state, gear position, race position. Channels have metadata including base units and display units. A channel value is a `(channelId, value, timestamp)` triple.

### Channel Definition
A saved configuration record describing a channel's behavior (units, deadband, evaluation type, etc.). Uses the `Definition` suffix per naming convention.

### Channel State
A runtime/status record for a channel (e.g., current value, last-updated time). Uses the `State` suffix per naming convention.

### Reserved Channel
A channel well-known to the application and shared across cars and cloud services without per-car definition. Enables streamlined use in dashboards and alarms, and can unlock special features (e.g., fuel range analysis when a fuel usage channel is present).

### Race Session
A bounded period of on-track activity (practice, qualifying, race stint, etc.) with a `start_time` and `end_time`. Sessions are managed externally (RedMist.racing integration, planned) and stored as metadata. Channel log data is **not** tagged with a session FK — queries correlate logs to sessions by time-range lookup.

### Race Event
A race weekend or competition event. Contains one or more Race Sessions. Managed by the same external integration as Race Sessions.

### Telemetry Stream
The single Redis Stream (`telemetry`) carrying all channel values — both car-sourced and cloud-sourced. Each message carries `carId`, `channelId`, `value`, `timestamp`, `source`, and `sendToCar` fields. Multiple services publish to and consume from this stream via separate consumer groups.

### sendToCar
A flag on a telemetry stream message indicating that CarGateway should forward the value to the car via its SignalR connection. Any cloud service may publish messages with this flag set; CarGateway is the sole forwarder.

---

## Cloud Service Topology

Three Kubernetes deployments:

### CarGateway
- Manages inbound SignalR connections from cars (OAuth 2.0 Client Credentials via Keycloak)
- Receives telemetry from cars and publishes to the Redis `telemetry` stream
- Consumes the `telemetry` stream and forwards messages with `sendToCar=true` to the appropriate car's SignalR connection
- Requires K8s sticky sessions (session affinity) on the ingress so a reconnecting car lands on the same pod
- SignalR transport is **WebSockets only** — long polling is disabled. If the pod holding a car's connection dies, the car reconnects, lands on a new pod, and re-initializes via the normal Config ID handshake

### WebApi
- Single cloud-facing HTTP/SignalR service for browser clients
- Hosts REST CRUD APIs: car management, channel definitions, alarm rules, dashboard configuration, race events/sessions, team/user management
- Hosts the real-time telemetry SignalR hub: subscribes to the Redis `telemetry` stream, maintains per-car current-value state in memory, pushes deltas to browser clients
- SignalR transport is **WebSockets only** — long polling is disabled
- Uses the ASP.NET Core SignalR Redis backplane (`AddStackExchangeRedis`) for multi-replica support
- Authenticates browser users via Keycloak (JWT bearer)

### ChannelProcessor
- Stateless background worker service; no inbound HTTP
- Runs as **multiple replicas**; horizontally scalable
- Hosted workers (all in the same executable):
  - **CloudChannelEvaluator** — evaluates cloud-side derived channels (math, tables, timers, counters, user conditions, logic) using the `Channels` project evaluators; publishes results back to the `telemetry` stream
  - **AlarmEvaluator** — evaluates alarm rules against incoming channel values; writes triggered alarms to Postgres
  - **ChannelLogger** — buffers incoming channel values and bulk-inserts them to Postgres in batches (≤500 ms or ≤500 rows); the primary write path for historical telemetry data
- **Stateful evaluator state** (timers, counters, condition latches, change filter) is stored in Redis using the `IStateRepository` implementations from the `Channels` project. This allows any replica to process any car's messages
- **ACK ordering rule:** a stream message must not be ACKed until all evaluator state writes and derived-value publishes are complete. The Redis Streams consumer group serializes delivery per consumer — the next message is not dispatched until the current one is ACKed — so this rule guarantees state consistency across replicas

---

## Telemetry Stream Design

- **Single stream**: `telemetry` (Redis Streams)
- **Message fields**: `carId`, `channelId`, `value`, `timestamp` (UTC), `source` (e.g., `"car"`, `"cloud:math"`, `"cloud:position"`), `sendToCar` (bool)
- **Consumer groups**: one per consuming service (`cargw`, `webapi`, `channelproc`)
- **Retention**: `MAXLEN ~` 50 000 entries (approximate trim); covers several minutes of data at peak rate — sufficient for consumer lag recovery without unbounded memory growth
- **Car events**: separate Redis pub/sub channel `car-events` carrying `CarConnected`, `CarDisconnected`, `CarConfigChanged`, and `CarConfigSynced` events

---

## Channel Value Log (Postgres)

Table: `ChannelLogs`

| Column | Type | Notes |
|---|---|---|
| `Id` | bigint | surrogate PK |
| `TeamId` | int | FK to Teams (tenant scope) |
| `CarId` | int | FK to Cars |
| `ChannelId` | int | FK to channel definitions |
| `Timestamp` | timestamptz | wall-clock time from stream message |
| `Value` | double | base-unit value |

- **No session FK on log rows.** Session correlation is done at query time via `WHERE Timestamp BETWEEN session.StartTime AND session.EndTime`.
- **Write pattern**: ChannelLogger buffers values in memory and flushes as bulk INSERT, up to every 500 ms or 500 rows.
- **Primary query index**: `(CarId, Timestamp)` covering index.
- **Volume**: manageable with plain Postgres (no TimescaleDB); ~16 racing hours per month per car.
- **Use cases**: live trend display in dashboards; historical data for anomaly detection model training.

---

## Repository Structure

Cloud services live under `services/cloud/`, mirroring the `services/car/` pattern:

```
services/
  car/          ← in-car services (existing)
  cloud/
    CarGateway/
    WebApi/
    ChannelProcessor/
  Channels/     ← shared evaluation logic (existing)
  Common/       ← shared models (existing)
```

---

## Car Configuration Sync

The car's in-car configuration (CAN mappings, channel definitions, logging settings) is identified by a **Configuration ID** (GUID) assigned when the configuration is created.

### Primary path (pre-connect)
The local `RaceManagementService` (pit laptop tool) automatically pushes the active car configuration to the WebApi cloud endpoint whenever it is updated or synced. The configuration is stored in Postgres keyed by its Configuration ID before the car arrives at the track.

### Fallback path (on-connect)
When a car establishes its SignalR connection to CarGateway, it sends its active Configuration ID in the connection handshake. CarGateway:
1. Queries Postgres for a configuration record matching that ID
2. **Found** → proceeds; publishes a `CarConnected` event to the `car-events` Redis pub/sub channel carrying `carId` and `configId`
3. **Not found** → invokes a SignalR hub method on the car to request the full configuration payload; writes the received configuration directly to Postgres; then proceeds as in step 2

CarGateway writes directly to Postgres in the fallback path (not via WebApi). See [ADR-0001](docs/adr/0001-cargw-direct-config-write.md).

### ChannelProcessor notification
ChannelProcessor subscribes to the `car-events` Redis pub/sub channel. On receiving `CarConnected`, it loads the cloud-side processing configuration for that car (cloud channel definitions, alarm rules) from Postgres into memory. On `CarConfigChanged` (published by WebApi after a save), it reloads the affected car's configuration. On `CarDisconnected`, it tears down in-memory state for that car.

---

## Race Event / Session Metadata

- `RaceEvents` and `RaceSessions` are stored as first-class entities with `StartTime`/`EndTime`.
- Populated initially by manual entry; planned integration with **RedMist.racing** for automatic population.
- Not coupled to the telemetry pipeline — the logging worker has no dependency on session state.
