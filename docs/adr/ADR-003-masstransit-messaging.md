# ADR-003: Adoption of MassTransit for Messaging Abstraction

## Status

Accepted

## Context

The platform follows a microservices architecture with bounded contexts (Identity, Orders, Notifications) that need to communicate asynchronously. Key requirements:

- **Loose coupling**: Services must not take direct dependencies on each other's APIs for event-driven workflows (e.g., Notifications needs to react to `OrderPlacedEvent` without the Orders service knowing about Notifications).
- **Transport portability**: The platform runs on both AWS (SNS/SQS) and Azure (Service Bus). The messaging code should not be rewritten for each cloud provider.
- **Reliability**: Messages must not be lost during transient broker outages. The system must support at-least-once delivery with idempotent consumers.
- **Local development**: Engineers need to run the full event-driven flow locally without cloud accounts, using RabbitMQ in Docker.

Alternatives considered:
- **Raw RabbitMQ.Client / Azure.Messaging.ServiceBus**: Maximum control but requires implementing serialization, retry policies, dead-letter handling, and consumer lifecycle management manually for each transport.
- **NServiceBus**: Mature but commercially licensed, adding cost friction for open-source reference material.
- **Dapr Pub/Sub**: Cloud-native but introduces a sidecar dependency and additional operational complexity.

## Decision

We adopt **MassTransit** as the messaging abstraction layer for all inter-service asynchronous communication. The implementation:

- **Transport configuration**: In local development and Docker Compose, MassTransit is configured with the **RabbitMQ** transport (`cfg.UsingRabbitMq`). In production, the transport is swapped to AWS SQS/SNS or Azure Service Bus via configuration — no consumer code changes required.
- **Event publishing**: The `MassTransitEventPublisher` class implements `IApplicationEventPublisher` (defined in the Application layer) by delegating to `IPublishEndpoint.Publish`. This keeps the Application layer transport-agnostic.
- **Consumer implementation**: `OrderPlacedConsumer` implements `IConsumer<OrderPlacedEvent>` and is auto-discovered via assembly scanning. Each consumer processes one event type.
- **Integration with Outbox**: MassTransit publishes events dispatched by the `OutboxProcessor` background service (see ADR-004), ensuring events are only published after successful persistence.
- **Docker Compose**: RabbitMQ 3.13 runs as a service with healthchecks; the Orders API depends on it with `condition: service_healthy`.

## Consequences

### Positive

- **Transport abstraction**: Switching from RabbitMQ (local) to SQS/SNS (AWS) or Service Bus (Azure) requires only configuration changes, not code changes.
- **Convention-based setup**: MassTransit auto-configures exchanges, queues, and subscriptions based on message type names, reducing boilerplate.
- **Built-in resilience**: Retry policies, circuit breakers, and dead-letter queues are configured declaratively.
- **Local-first development**: Engineers spin up `docker-compose up` and get a fully functional message broker without cloud credentials.

### Negative

- **Abstraction cost**: MassTransit's conventions (exchange naming, topology creation) can be opaque and surprising when debugging message routing issues.
- **Version coupling**: Major MassTransit upgrades occasionally introduce breaking changes to transport configuration APIs.
- **Learning curve**: Engineers must understand both MassTransit concepts (sagas, consumers, middleware) and the underlying broker semantics for production debugging.
