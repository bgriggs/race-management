# Race Management - Copilot Instructions

This application provides tools and services for team road racing management, including communication with cars, collection of car telemetry, team display dashboards, alarms, and planning tools. It supports teams with one or multiple cars.

## Tech Stack

- **Frontend:** Angular with TypeScript
- **Backend services:** C#
- **Database:** PostgreSQL
- **Caching:** Redis
- **Streaming:** Redis
- **Real-time communication:** SignalR

## Hosting

- Cloud: Digital Ocean, deployed to Kubernetes
- CDN: Bunny CDN (for cloud UI)

## Hardware

- In-car compute: Raspberry Pi
- CAN Bus interface: PiCAN 2 (CAN Bus Interface for Raspberry Pi)

## Communications

- Primary car-to-cloud: Cellular
- Planned: Low-frequency WiFi (e.g., HaLow) from car to trackside/pits; pit-to-cloud via cellular or Starlink

## Cloud Services

- **Car Telemetry Processor** — maintains connections with cars via SignalR and processes telemetry for use across the application (e.g., dashboards)
- **Alarm Processor** — evaluates rules to generate alerts on car telemetry data (e.g., high temperature)
- **Channel Processor** — runs cloud-side channels, such as math channels or channels sourced from third-party integrations (e.g., race position data)

## Channels

A channel represents a discrete piece of data — e.g., sensor temperature, button state, gear position, race position. Channels have metadata including base units and display units. Channel data can originate from the car or from the cloud.

Channels are either **user-defined** or **reserved**.

### Reserved Channels

Reserved channels are well-known by the application and can be shared across cars and cloud services. They enable streamlined usage in dashboards and alarms without per-car definitions, and can unlock special features when present (e.g., fuel range analysis when a fuel usage channel is available).

### Channel Class Naming Conventions

- Saved behavior/configuration classes should use the suffix **'Definition'**.
- Runtime/status classes should use the suffix **'State'**.

### Channel Repository Abstraction Guidelines

When evolving Channels repository abstractions, keep implementation changes within the Channels project and tests, not service-tier implementations.

## Configuration UI

A laptop-based configuration tool (Angular frontend + locally installed C# backend service) for setting up the in-car application. Configuration includes channels, math, login, logging, and CAN Bus communications. Configuration is exchanged with the device over Ethernet. A copy is saved to the cloud; multiple versions can exist but only one is active on the car at a time.

## In-Car Application

A systemd C# service running on the Raspberry Pi, responsible for:
- Reading and writing CAN Bus data
- Connecting to the cloud or trackside services
- Transmitting telemetry data

## Repository Structure

- `services/Channels/` — Channel definitions, values, and repository interfaces/implementations
  - `Counters/` — Counter channel evaluation logic
  - `Logic/` — Logic/comparison channel evaluation
  - `Math/` — Math channel evaluation
  - `Tables/` — Table/interpolation channel evaluation
  - `Timers/` — Timer channel evaluation
  - `UserConditions/` — User-defined condition evaluation
- `services/ChannelsTests/` — Unit tests for channel evaluation
- `services/Common/` — Shared models and utilities (e.g., car configuration, CAN Bus)
- `services/RaceManagementService/` — ASP.NET Core web API entry point

## UI Shared Library Guidance

The Angular UI uses two applications in one workspace (`ui/race-management-cloud` and `ui/race-management-local`) plus a shared library area under `ui/shared-ui`.

When generating or modifying frontend code:
- Put reusable UI components in `ui/shared-ui/src/lib`.
- Put shared UI styles with the shared elements in `ui/shared-ui` (for example component-level styles and reusable shared style definitions).
- Prefer global shared styles/tokens from `ui/shared-ui` where possible before adding new component-local styles.
- Avoid duplicating shared components or shared styles in both app folders.
- Do not duplicate style definitions in app-specific components when an equivalent shared style already exists.
- Keep app-specific concerns (routing, app shell, and environment-specific wiring) in each app project.

### Shared UI Checklist

- Confirm the UI element is reusable across cloud and local apps.
- Create the component in `ui/shared-ui/src/lib`.
- Keep shared styles with the shared component in `ui/shared-ui`.
- Export from `ui/shared-ui/src/public-api.ts` when a public shared import is needed.
- Keep shared components presentation-focused (inputs/outputs), with environment-specific behavior in app-level services.
- Remove duplicated app-local copies after adopting the shared component.
- Validate both apps build from `ui/` after integration.
