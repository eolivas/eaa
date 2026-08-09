# ADR-004: Use of the Outbox Pattern for Reliable Event Delivery

## Status

Accepted

## Context

In the {Entity} service, domain events (e.g., `{Entity}PlacedEvent`) must be published to the message broker after a successful state change. A naive implementation — save to database, then publish to broker — creates a dual-write problem:

1. **Database succeeds, broker publish fails**: The {entity} is persisted but downstream services (Notifications) never receive the event. The system becomes inconsistent.
2. **Broker publish succeeds, database fails**: The event is published but the state change is rolled back. Consumers act on phantom events.

Neither distributed transactions (2PC) nor "hope-based" retry adequately solve this in a microservices context. The system needs **exactly-once semantics for the write side** (guaranteed delivery of events corresponding to persisted state changes).

## Decision

We implement the **Transactional Outbox Pattern** in the Infrastructure layer:

1. **Outbox table**: An `outbox_messages` table (entity: `OutboxMessage`) stores pending events with columns: `Id`, `EventType`, `Payload` (JSON-serialized domain event), `OccurredAt`, and `ProcessedAt` (nullable).

2. **Atomic write**: In `{SolutionName}DbContext.SaveChangesAsync`, domain events raised by aggregates are intercepted, serialized to `OutboxMessage` rows, and inserted in the **same database transaction** as the entity state change. After save, domain events are cleared from the aggregate.

3. **Background processor**: `OutboxProcessor` is a hosted background service that polls the `outbox_messages` table on a configurable interval (default 5 seconds) for rows where `ProcessedAt IS NULL AND FailedAt IS NULL`. For each batch of unprocessed messages (configurable batch size, default 20, ordered by `OccurredAt`), it:
   - Deserializes the event payload
   - Publishes via MassTransit `IPublishEndpoint` (including the original correlation ID in message headers)
   - Sets `ProcessedAt = DateTime.UtcNow` in the same transaction as the publish acknowledgement
   - Emits OpenTelemetry metrics: `outbox.messages.processed` counter, `outbox.messages.failed` counter, `outbox.message.duration_ms` histogram

4. **Failure handling**: If publication fails (type resolution failure, deserialization error, or broker unavailability), the message's `RetryCount` is incremented and the message remains eligible for the next polling cycle. When `RetryCount` reaches the configurable maximum (default 5), the message is dead-lettered: `FailedAt` is set to the current timestamp and `FailureReason` records the error. Dead-lettered messages are excluded from subsequent polling via the `WHERE FailedAt IS NULL` filter.

5. **Retention**: A separate `OutboxRetentionService` background service runs on a configurable interval (default 60 minutes) and deletes processed messages older than the configurable retention period (default 7 days). Deletion is performed in batches (default 500) to avoid long-running transactions that could block the processor.

6. **Idempotency**: Consumers are expected to be idempotent because at-least-once delivery means the same event may be published more than once if the processor crashes between publish and commit.

## Consequences

### Positive

- **Guaranteed delivery**: Events are never lost — if the database write succeeds, the event will eventually be published (at-least-once semantics).
- **No distributed transactions**: Eliminates the need for 2PC or XA transactions between the database and message broker.
- **Auditability**: The `outbox_messages` table serves as an audit log of all domain events with timestamps.
- **Resilience**: Transient broker outages do not cause data loss; unprocessed messages accumulate and are drained when the broker recovers.

### Negative

- **Eventual consistency**: There is a delay (up to the polling interval + processing time) between the state change and event delivery. Downstream services see events seconds after the fact, not immediately.
- **Polling overhead**: The background processor queries the database on a configurable interval regardless of activity. Under low-traffic conditions this is wasted I/O (though the query is indexed and lightweight).
- **Consumer idempotency requirement**: All consumers must handle duplicate events gracefully, adding complexity to downstream services.
