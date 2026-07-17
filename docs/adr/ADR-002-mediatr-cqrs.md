# ADR-002: Choice of MediatR for CQRS

## Status

Accepted

## Context

The application layer needs a mechanism to decouple API endpoints from business logic handlers. Specifically:

- **Command/Query Responsibility Segregation (CQRS)**: Write operations (commands) and read operations (queries) have different performance profiles, validation needs, and side effects. Treating them uniformly leads to bloated service classes.
- **Cross-cutting concerns**: Logging, validation, performance tracking, and transaction management need to be applied consistently across all handlers without duplicating code in each one.
- **Testability**: Handlers must be independently testable with mocked dependencies, without needing the full ASP.NET Core pipeline.
- **Single Responsibility**: Each handler should do exactly one thing — process one command or answer one query.

Alternatives considered:
- **Direct service injection**: Simple but leads to large service classes violating SRP, and cross-cutting concerns must be manually applied.
- **Custom mediator**: Full control but significant maintenance overhead and reinvention of well-solved patterns.
- **Wolverine**: More opinionated and tightly coupled to its own hosting model.

## Decision

We adopt **MediatR** as the in-process mediator for dispatching commands and queries in the Application layer. The implementation follows these conventions:

- **Commands** implement `IRequest<TResponse>` (e.g., `PlaceOrderCommand : IRequest<Guid>`). Commands represent intent to change state.
- **Queries** implement `IRequest<TResponse>` (e.g., `GetOrderQuery : IRequest<OrderDto?>`). Queries are side-effect-free reads.
- **Handlers** implement `IRequestHandler<TRequest, TResponse>` with a single `Handle` method.
- **Pipeline Behaviours** implement `IPipelineBehavior<TRequest, TResponse>` and are registered in order:
  1. `LoggingBehaviour` (outermost) — logs request name and elapsed time at `Information` level.
  2. `ValidationBehaviour` (innermost, before handler) — runs all registered `IValidator<TRequest>` instances via FluentValidation; throws `ValidationException` if any failures, preventing handler execution.

Registration in `Program.cs`:
```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<PlaceOrderHandler>());
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
```

API endpoints dispatch via `ISender.Send(command)` — they never reference handlers directly.

## Consequences

### Positive

- **Thin endpoints**: API endpoints contain only request mapping and `ISender.Send()` — all logic lives in testable handlers.
- **Uniform cross-cutting behaviour**: Logging and validation are applied to every command/query automatically via the pipeline, eliminating per-handler boilerplate.
- **Handler isolation**: Each handler has a single responsibility and can be tested in isolation with Moq-based mocks for repositories and publishers.
- **Discoverability**: The `IRequest`/`IRequestHandler` convention makes it easy to find all commands, queries, and their handlers via IDE navigation.

### Negative

- **Implicit dispatch**: The mediator hides the direct call path from endpoint to handler, which can make debugging and "Find All References" less straightforward for newcomers.
- **Runtime resolution**: Handler resolution is DI-based at runtime; misconfigured registrations produce runtime errors rather than compile-time failures.
- **Single-process constraint**: MediatR is in-process only. Cross-service communication still requires a message broker (see ADR-003).
