# ADR-008: Microservices Architecture with Bounded Contexts

## Status

Accepted

## Context

The platform must support multiple independent business capabilities (identity management, core business logic, notifications, AI tool orchestration) that evolve at different rates, scale independently, and are maintained by different teams. Key requirements:

- **Independent deployability**: A change to notification logic should not require redeploying the core business service.
- **Team autonomy**: Teams owning different bounded contexts should be able to make technology choices within their service without affecting others.
- **Scaling granularity**: Read-heavy services (BFF) and event-processing services (Notifications) have fundamentally different resource profiles.
- **Fault isolation**: A failure in one service (e.g., notification delivery) must not cascade to the core business domain.
- **Technology evolution**: Individual services may need to migrate databases, switch message brokers, or adopt new frameworks independently.

Alternatives considered:
- **Monolithic application**: Simpler to develop initially but creates deployment coupling, shared database bottlenecks, and team coordination overhead as the platform grows.
- **Modular monolith**: Provides logical separation but still deploys as a single unit, limiting independent scaling and fault isolation.
- **Serverless functions**: Maximum granularity but introduces cold-start latency, vendor lock-in, and complex orchestration for stateful workflows.

## Decision

We adopt a **microservices architecture** organized around Domain-Driven Design bounded contexts. Each service:

1. **Owns its bounded context**: One aggregate root (or a small set of related aggregates) per service. No shared domain models across service boundaries.

2. **Owns its data**: Each service has its own database schema with distinct credentials. No direct cross-service database access.

3. **Communicates asynchronously by default**: Domain events flow via MassTransit over SNS/SQS (AWS), Service Bus (Azure), or RabbitMQ (local). Synchronous HTTP calls are limited to the BFF and MCP Gateway aggregation layer.

4. **Deploys independently**: Each service has its own Dockerfile, CI pipeline, container image, and ECS task definition / Container App revision. Deploying one service does not require deploying another.

5. **Follows the same internal structure**: Every service uses the four-layer Clean Architecture (Domain → Application → Infrastructure → Api) established in ADR-001, ensuring consistent onboarding across teams.

### Service Catalog

| Service | Role | Communication Style |
|---------|------|-------------------|
| Identity Service | User management, authentication, token issuance | Publishes events, responds to sync queries via BFF |
| {Entity} Service | Core business domain (primary aggregate) | Publishes events, responds to sync queries via BFF |
| Notifications Service | Event-driven side effects (email, SMS, push) | Consumes events only — no API |
| MCP Gateway | AI tool orchestration, proxies to domain services | Sync calls to other services |
| BFF Service | Client aggregation, response composition | Sync calls to other services |

### Inter-Service Communication Rules

- **Async events** for state-change notifications (Observer pattern via outbox + MassTransit)
- **Sync HTTP** only through BFF/MCP Gateway for request-response aggregation
- **No direct service-to-service HTTP** between domain services
- **Event schemas** are owned by the publishing service — consumers adapt to the publisher's contract

## Consequences

### Positive

- **Independent deployment cycles**: Each service releases on its own cadence without coordinating with other teams.
- **Fault isolation**: A Notifications service failure does not affect the core business domain's ability to process requests.
- **Targeted scaling**: The BFF can scale horizontally for read traffic while the Notifications service scales for event throughput.
- **Technology flexibility**: A service can migrate from PostgreSQL to DynamoDB without impacting other services.
- **Clear ownership**: Each bounded context has a single responsible team with full autonomy over its internals.

### Negative

- **Distributed complexity**: Network failures, eventual consistency, and distributed debugging are inherently more complex than in-process calls.
- **Data consistency**: Cross-service consistency requires eventual consistency patterns (outbox, sagas) rather than ACID transactions.
- **Operational overhead**: Each service needs its own CI pipeline, health checks, monitoring dashboards, and alert rules.
- **Duplication**: Some shared concepts (e.g., Money value object) are duplicated across services rather than shared via libraries, to preserve independence.
- **Testing complexity**: End-to-end flows spanning multiple services require integration environments or contract testing.
