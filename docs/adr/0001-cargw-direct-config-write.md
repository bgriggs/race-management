# ADR-0001: CarGateway writes car configuration directly to Postgres on fallback fetch

**Status:** Accepted  
**Date:** 2026-05-16

## Context

When a car connects to CarGateway and its Configuration ID is not found in Postgres (the primary sync path via the local RaceManagementService failed), CarGateway must fetch the full configuration from the car over SignalR and persist it to Postgres so downstream services can use it.

Two options were considered for the write path:

- **Option A:** CarGateway POSTs the received configuration to the WebApi `/config` endpoint. WebApi is the single authoritative write path for all configuration data.
- **Option B:** CarGateway writes the received configuration directly to Postgres.

## Decision

**Option B** — CarGateway writes directly to Postgres.

## Rationale

- CarGateway already has a Postgres connection to perform the configuration ID lookup. Adding a direct write does not introduce a new infrastructure dependency.
- The fallback path is rare (it only fires when the local RaceManagementService failed to pre-sync). Introducing a synchronous inter-service HTTP call (CarGateway → WebApi) in the connection handshake adds latency and a failure mode in a path that is already a recovery scenario.
- The configuration written by CarGateway in the fallback path is an exact copy of what the car reports. It requires no validation logic beyond storage — unlike configurations authored by users in the UI, which WebApi owns and validates.

## Consequences

- CarGateway holds a Postgres write responsibility for one specific table (car configurations). This is a deliberate, narrow exception to WebApi being the primary config API.
- If the configuration write schema changes, CarGateway must be updated alongside WebApi.
- WebApi should expose an endpoint for the UI to reflect that a configuration was synced via the fallback path (e.g., after CarGateway writes it, it should appear in the cloud UI's configuration list). CarGateway should publish a `CarConfigSynced` event to `car-events` after writing so WebApi can notify connected browser clients.
