# ADR-004: Use of the Outbox Pattern for Reliable Event Delivery

## Status

Accepted

## Context

In the Orders service, domain events (e.g., `OrderPlacedEvent`) must be published to the message broker after a successful state change. A naive implementation — save to database, then publish to broker — creates a dual-write problem:

1. **Database succeeds, broker publish fails**: The order is persisted but downstream services (Notifications) never receive the event. The system becomes inconsistent.
2. **Broker publish succeeds, database fails**: The event is published but the state change is rolled back. Consumers act on phantom events.

Neither distributed transactions (2PC) nor "hope-based" retry adequately solve this in a microservices context. The system needs **exactly-once semantics for the write side** (guaranteed delivery of events corresponding to persisted state changes).

## Decision

We implement the **Transactional Outbox Pattern** in the Orders service Infrastructure layer:

1. **Outbox table**: An `outbox_messages` table (entity: `OutboxMessage`) stores pending events with columns: `Id`, `EventType`, `Payload` (JSON-serialized domain event), `OccurredAt`, and `ProcessedAt` (nullable).

2. **Atomic write**: In `OrdersDbContext.SaveChangesAsync`, domain events raised by aggregates are intercepted, serialized to `OutboxMessage` rows, and inserted in the **same database transaction** as the entity state change. After save, domain events are cleared from the aggregate.

3. **Background processor**: `OutboxProcessor` is a hosted background service that polls the `outbox_messages` table every 5 seconds for rows where `ProcessedAt IS NULL`. For each unprocessed message, it:
   - Deserializes the event payload
   - Publishes via MassTransit `IPublishEndpoint`
   - Sets `ProcessedAt = DateTime.UtcNow` in the same transaction as the publish acknowledgement

4. **Failure handling**: If publication fails (broker unavailable, serialization error), the transaction is rolled back, `ProcessedAt` remains null, and the message is retried on the next polling cycle. Errors are logged at `Error` level with the `OutboxMessage.Id` for tracing.

5. **Idempotency**: Consumers are expected to be idempotent because at-least-once delivery means the same event may be published more than once if the processor crashes between publish and commit.

## Consequences

### Positive

- **Guaranteed delivery**: Events are never lost — if the database write succeeds, the event will eventually be published (at-least-once semantics).
- **No distributed transactions**: Eliminates the need for 2PC or XA transactions between the database and message broker.
- **Auditability**: The `outbox_messages` table serves as an audit log of all domain events with timestamps.
- **Resilience**: Transient broker outages do not cause data loss; unprocessed messages accumulate and are drained when the broker recovers.

### Negative

- **Eventual consistency**: There is a delay (up to the polling interval + processing time) between the state change and event delivery. Downstream services see events seconds after the fact, not immediately.
- **Polling overhead**: The background processor queries the database every 5 seconds regardless of activity. Under low-traffic conditions this is wasted I/O (though the query is indexed and lightweight).
- **Table growth**: The outbox table grows indefinitely unless a retention policy (archive or delete processed rows after N days) is implemented separately.
- **Consumer idempotency requirement**: All consumers must handle duplicate events gracefully, adding complexity to downstream services.
