# ADR-0002: ChannelProcessor stores evaluator state in Redis for horizontal scalability

**Status:** Accepted  
**Date:** 2026-05-16

## Context

ChannelProcessor evaluates cloud-side derived channels (timers, counters, user conditions, logic statements, math). Several evaluator types are **stateful** — a timer knows when it started, a counter holds a running total, a condition latch knows its current armed state. This state must survive across individual stream messages.

ChannelProcessor runs as multiple K8s replicas. A Redis Streams consumer group distributes messages across replicas, meaning consecutive messages for the same car may be processed by different replicas. If evaluator state lives only in the processing replica's memory, state is lost when a message moves to a different replica.

Two options were considered:

- **Option A: In-memory state, single replica** — simple; no state distribution problem. Acceptable at small scale but not horizontally scalable.
- **Option B: Redis-backed state, multiple replicas** — evaluator state is stored in Redis using `IStateRepository` implementations from the `Channels` project; any replica can process any car's messages.

## Decision

**Option B** — evaluator state stored in Redis; multiple replicas supported.

## Rationale

- The `Channels` project already defines `IStateRepository<TId, TState>` as the abstraction for runtime state. In-car uses in-memory implementations; cloud ChannelProcessor gets Redis implementations. This is the exact use case the abstraction was designed for.
- Redis Streams consumer group semantics provide the necessary ordering guarantee: a message is not delivered to the next consumer until the current consumer ACKs it. This serializes delivery per message and prevents two replicas from processing the same car's messages concurrently.
- Horizontal scalability is valuable for multi-team scenarios where many cars may be active simultaneously.

## Consequences

- **ACK ordering rule (hard constraint):** a stream message must not be ACKed until evaluator state writes to Redis and derived-value publishes to the telemetry stream are both complete. Violating this rule allows a second replica to read stale state.
- Redis implementations of all stateful channel repositories (`ITimerStateRepository`, `ICounterStateRepository`, `IUserConditionStateRepository`, `ILogicStateRepository`, change-filter state) must be built in the `Channels` project or `ChannelProcessor`.
- State keys must be scoped by `(teamId, carId, channelId)` to maintain tenant isolation in Redis.
- If a ChannelProcessor pod is terminated mid-message (before ACK), the consumer group will redeliver the message to another replica after the visibility timeout. That replica will read the last successfully written Redis state and reprocess — idempotent for most evaluators, acceptable for stateful ones (a timer may advance slightly; a counter may double-count a single value). This is a known, accepted trade-off.
