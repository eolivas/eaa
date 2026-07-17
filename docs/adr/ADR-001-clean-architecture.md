# ADR-001: Adoption of Clean Architecture

## Status

Accepted

## Context

The enterprise platform requires a long-lived, maintainable architecture that supports multiple teams working in parallel across different layers of the system. Key concerns include:

- **Testability**: Domain and business logic must be unit-testable without infrastructure dependencies (databases, message brokers, HTTP clients).
- **Independent deployability**: Changes to persistence or messaging technology should not ripple into business rules.
- **Onboarding velocity**: New engineers need a consistent, well-documented structure to navigate unfamiliar services quickly.
- **Technology evolution**: The platform must accommodate swapping infrastructure components (e.g., migrating from SQL Server to PostgreSQL, or RabbitMQ to AWS SNS/SQS) without rewriting domain or application code.

Traditional layered architectures (UI → Business → Data) create tight coupling between business logic and data access, making it expensive to change either in isolation.

## Decision

We adopt **Clean Architecture** (Robert C. Martin) as the structural foundation for all backend services, realized as a four-project structure per service:

1. **Orders.Domain** — Aggregates, entities, value objects, domain events, repository interfaces. Zero external NuGet dependencies. The innermost layer that defines the ubiquitous language.
2. **Orders.Application** — CQRS command/query handlers (MediatR), pipeline behaviours (validation, logging), DTOs, and application-level abstractions (`IApplicationEventPublisher`). Depends only on Domain.
3. **Orders.Infrastructure** — EF Core persistence, MassTransit messaging, HTTP clients, caching decorators, and all external I/O. Implements interfaces defined in Domain and Application. Depends on Domain and Application.
4. **Orders.Api** — ASP.NET Core Minimal API endpoints, middleware (exception handling), authentication/authorization configuration, and the composition root (`Program.cs`). Depends on all inner layers.

The **dependency rule** is strictly enforced: dependencies point inward only. Domain has no knowledge of Application, Infrastructure, or Api. Application has no knowledge of Infrastructure or Api. This is validated at build time via NetArchTest architecture tests in `Orders.Architecture.Tests`.

## Consequences

### Positive

- **Domain isolation**: Domain logic is pure C# with no framework dependencies, enabling fast, deterministic unit tests without mocks or test doubles.
- **Substitutability**: Infrastructure implementations (repositories, publishers, HTTP clients) can be swapped by changing DI registrations without touching business rules.
- **Parallel development**: Teams can work on Domain, Application, and Infrastructure independently with clearly defined contracts at layer boundaries.
- **Architecture enforcement**: NetArchTest rules (Property 1) run on every CI build, preventing accidental dependency violations from being merged.

### Negative

- **Indirection overhead**: Simple CRUD operations still traverse multiple layers (endpoint → handler → repository), which adds ceremony for trivial features.
- **Initial learning curve**: Engineers unfamiliar with Clean Architecture require onboarding to understand the layer separation and where to place new code.
- **Project proliferation**: Each service produces four source projects plus up to five test projects, increasing solution complexity.
