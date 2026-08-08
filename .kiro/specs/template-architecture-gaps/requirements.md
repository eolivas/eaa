# Requirements Document

## Introduction

This document defines requirements for closing all architectural gaps in the Enterprise Application Architecture (EAA) template. The template documents a Clean Architecture with CQRS, DDD, MassTransit messaging, the Outbox Pattern, and EF Core — but the implementation has significant gaps relative to what the ADRs and architectural documentation promise. This spec covers completing the template so it fully implements its own architectural vision across messaging, persistence, observability, security, resilience, testing, CI/CD, and frontend-backend integration.

## Glossary

- **Template**: The EAA .NET 8 solution template that generates production-ready enterprise service scaffolding.
- **Orders_API**: The ASP.NET Core Minimal API host project (composition root) serving HTTP endpoints and MCP tools.
- **Infrastructure_Layer**: The Orders.Infrastructure project implementing persistence, messaging, caching, and external HTTP integrations.
- **Outbox_Processor**: The background service that polls the outbox_messages table and publishes domain events via MassTransit.
- **MassTransit_Transport**: The configured message broker transport (RabbitMQ for local, SQS/SNS or Service Bus for production).
- **Health_Check_System**: ASP.NET Core health check middleware providing liveness and readiness probes.
- **OTEL_Collector**: OpenTelemetry Collector instance that receives traces, metrics, and logs from application services.
- **Correlation_ID**: A unique identifier propagated across HTTP requests and message handlers to enable distributed tracing.
- **EF_Migrations**: Entity Framework Core code-first migration files that version-control database schema changes.
- **Architecture_Tests**: NetArchTest-based tests that enforce Clean Architecture dependency rules at build time.
- **Resilience_Pipeline**: Microsoft.Extensions.Http.Resilience pipeline providing retry, circuit breaker, and timeout policies.

## Requirements

### Requirement 1: MassTransit RabbitMQ Transport Configuration

**User Story:** As a developer using the template, I want MassTransit configured with the RabbitMQ transport for Docker Compose, so that inter-service messaging works with a real broker locally instead of the InMemory transport.

#### Acceptance Criteria

1. WHEN the application starts in the Docker Compose environment, THE MassTransit_Transport SHALL connect to the RabbitMQ broker using connection settings from configuration (host, username, password).
2. WHEN the RabbitMQ broker is unavailable at startup, THE Orders_API SHALL retry the connection with exponential backoff (initial interval of 1 second, doubling per attempt) for a configurable number of attempts (default: 5) before terminating the application with a non-zero exit code and a logged error message indicating the broker is unreachable.
3. THE MassTransit_Transport SHALL auto-configure exchanges, queues, and subscriptions based on MassTransit naming conventions.
4. WHEN a consumer throws an exception, THE MassTransit_Transport SHALL retry the message delivery using a configurable retry policy (default: 3 retries with exponential backoff starting at 1 second and doubling per attempt, with a maximum interval of 8 seconds).
5. IF a message fails all retry attempts, THEN THE MassTransit_Transport SHALL move the message to a dead-letter queue named with the `_error` suffix appended to the source queue name, preserving the original message headers and body for troubleshooting.

### Requirement 2: Outbox Pattern Completion

**User Story:** As a developer using the template, I want the Outbox Pattern to be fully production-ready, so that domain events are reliably delivered without data loss.

#### Acceptance Criteria

1. THE Outbox_Processor SHALL retrieve unprocessed messages in batches of a configurable size (default: 20, minimum: 1, maximum: 1000), ordered by OccurredAt ascending, to reduce database round-trips.
2. IF a message fails processing (type resolution failure, deserialization failure, or publish failure) and the message's retry count has not reached the configurable maximum (default: 5), THEN THE Outbox_Processor SHALL increment the message's retry count and leave the message unprocessed for the next polling cycle.
3. IF a message's retry count reaches the configurable maximum (default: 5), THEN THE Outbox_Processor SHALL mark the message as failed by recording a failure reason and the failure timestamp, and SHALL exclude the message from subsequent processing cycles.
4. THE Infrastructure_Layer SHALL provide a retention cleanup mechanism that runs on a configurable schedule (default: every 60 minutes) and deletes outbox messages where ProcessedAt is not null and ProcessedAt is older than a configurable number of days (default: 7).
5. THE Outbox_Processor SHALL expose the following metrics: a counter of messages successfully processed, a counter of messages that reached the maximum retry count, and a histogram of per-message processing duration in milliseconds (measured from the start of deserialization to the completion of the publish acknowledgement).

### Requirement 3: EF Core Migrations Infrastructure

**User Story:** As a developer using the template, I want EF Core migrations set up with an initial migration, so that database schema changes are version-controlled and applied consistently.

#### Acceptance Criteria

1. THE Infrastructure_Layer SHALL contain an initial EF Core migration that creates the orders table (with columns for Id, CustomerId, Status), the order_lines table (with columns for Id, OrderId foreign key, ProductId, Quantity, UnitPrice_Amount, UnitPrice_Currency), and the outbox_messages table (with columns for Id, EventType, Payload, OccurredAt, ProcessedAt) matching the entity type configurations.
2. WHEN the Orders_API starts in the Development environment, THE Orders_API SHALL apply pending migrations automatically on startup before accepting HTTP requests.
3. IF migration application fails during Development startup, THEN THE Orders_API SHALL log an error and terminate the host process with a non-zero exit code.
4. WHEN the Orders_API starts in a non-Development environment, THE Orders_API SHALL check for pending migrations and log a warning message indicating the count of unapplied migrations without applying them, and SHALL continue startup normally.
5. THE Infrastructure_Layer SHALL include a design-time DbContext factory that implements IDesignTimeDbContextFactory<OrdersDbContext> to support running `dotnet ef migrations` commands from the CLI without the API host project.

### Requirement 4: Database Seeding

**User Story:** As a developer using the template, I want a database seeding mechanism for development, so that the local environment starts with representative data.

#### Acceptance Criteria

1. WHEN the Orders_API starts in the Development environment and the orders table contains zero records, THE Orders_API SHALL seed the database with at least 1 order in each lifecycle state (Pending, Placed, Shipped, Cancelled), each order containing at least 1 order line.
2. THE seeding mechanism SHALL use domain factory methods (Order.Create, Order.Place, Order.Cancel) to create seed data for states reachable through public domain methods, ensuring domain invariants are respected. For the Shipped state, which has no public domain transition method, the seeding mechanism SHALL set the status directly, bypassing domain methods.
3. WHILE the application is running in a non-Development environment, THE seeding mechanism SHALL remain inactive and execute no database writes.
4. IF the orders table already contains one or more records when the application starts in the Development environment, THEN THE seeding mechanism SHALL skip seeding and leave existing data unchanged.

### Requirement 5: Application Health Checks

**User Story:** As a developer using the template, I want health check endpoints, so that orchestrators and load balancers can determine service readiness.

#### Acceptance Criteria

1. THE Orders_API SHALL expose a `GET /health/live` endpoint that returns HTTP 200 with a JSON body containing a `status` field set to `"Healthy"` when the process is running (liveness probe).
2. THE Orders_API SHALL expose a `GET /health/ready` endpoint that returns HTTP 200 with a JSON body containing a `status` field set to `"Healthy"` only when both the PostgreSQL database and RabbitMQ broker respond successfully to a connection-level check within 5 seconds each (readiness probe).
3. WHEN the PostgreSQL database does not respond to a connection-level check within 5 seconds, THE Health_Check_System SHALL return HTTP 503 on the readiness endpoint with a JSON body containing a `status` field set to `"Unhealthy"` and an `entries` object where the PostgreSQL entry's `status` is `"Unhealthy"`.
4. WHEN the RabbitMQ broker does not respond to a connection-level check within 5 seconds, THE Health_Check_System SHALL return HTTP 503 on the readiness endpoint with a JSON body containing a `status` field set to `"Unhealthy"` and an `entries` object where the RabbitMQ entry's `status` is `"Unhealthy"`.
5. IF both the PostgreSQL database and RabbitMQ broker are unreachable simultaneously, THEN THE Health_Check_System SHALL return HTTP 503 on the readiness endpoint with a JSON body where both dependency entries report `"Unhealthy"` status.
6. THE health check endpoints SHALL NOT require authentication.

### Requirement 6: Observability — OpenTelemetry Metrics and Local Backends

**User Story:** As a developer using the template, I want OpenTelemetry metrics collection and local observability backends, so that I can monitor application behavior during development.

#### Acceptance Criteria

1. THE Orders_API SHALL export OpenTelemetry metrics (ASP.NET Core HTTP request duration and count, EF Core query duration, and custom MCP metrics including mcp.tokens.input, mcp.tokens.output, and mcp.cache.hits) via the OTLP exporter configured to send to the OTEL_Collector service endpoint.
2. THE Docker Compose environment SHALL include an OTEL_Collector service that receives OTLP data (traces, metrics, and logs) on port 4317 (gRPC) and forwards them to the configured trace and metrics backends.
3. THE Docker Compose environment SHALL include a Jaeger backend exposing a UI on port 16686 for viewing distributed traces locally.
4. THE Docker Compose environment SHALL include a Prometheus backend exposing a query UI on port 9090 for querying application metrics locally.
5. THE Orders_API SHALL add the `WithMetrics` configuration to the OpenTelemetry builder alongside the existing `WithTracing`, registering ASP.NET Core instrumentation and the "Orders.Mcp" custom meter.
6. WHEN the OTEL_Collector service starts, THE Docker Compose environment SHALL configure health checks so that the Orders_API service starts only after the OTEL_Collector is ready to receive data.
7. IF the OTEL_Collector service is unreachable, THEN THE Orders_API SHALL continue to start and serve requests without failing, dropping telemetry data until the collector becomes available.

### Requirement 7: Distributed Correlation IDs

**User Story:** As a developer using the template, I want correlation IDs propagated across HTTP requests and message consumers, so that I can trace a request through all processing stages.

#### Acceptance Criteria

1. WHEN an HTTP request arrives without a `X-Correlation-Id` header, THE Orders_API SHALL generate a new GUID and attach it to the request context.
2. WHEN an HTTP request arrives with a `X-Correlation-Id` header containing a valid GUID value, THE Orders_API SHALL use the provided value as the correlation ID for that request.
3. IF an HTTP request arrives with a `X-Correlation-Id` header that is empty or not a valid GUID, THEN THE Orders_API SHALL ignore the provided value, generate a new GUID, and use it as the correlation ID.
4. THE Orders_API SHALL return the active correlation ID in the `X-Correlation-Id` response header on every HTTP response.
5. THE Orders_API SHALL include the correlation ID in all log entries produced during request processing by pushing a `CorrelationId` property to the Serilog log context.
6. WHEN the Outbox_Processor publishes a message, THE Outbox_Processor SHALL include the original correlation ID in the MassTransit message headers using the key `X-Correlation-Id`.
7. WHEN a MassTransit consumer receives a message with a `X-Correlation-Id` header, THE consumer SHALL extract the correlation ID from that header and push a `CorrelationId` property to the Serilog log context for the duration of the consumer scope.

### Requirement 8: CORS Configuration

**User Story:** As a developer using the template, I want CORS configured for the frontend origin, so that the React SPA can communicate with the API without browser security blocks.

#### Acceptance Criteria

1. WHILE the application is running in the Development environment, THE Orders_API SHALL allow CORS requests from `http://localhost:3000` (the frontend dev server origin).
2. THE Orders_API SHALL allow CORS requests only for origins listed in the application configuration, and SHALL reject cross-origin requests from origins not present in the configured list by omitting CORS response headers.
3. THE Orders_API SHALL allow the HTTP methods GET, POST, PUT, DELETE, and OPTIONS in CORS preflight responses.
4. THE Orders_API SHALL allow the headers Authorization, Content-Type, and X-Correlation-Id in CORS requests.
5. THE Orders_API SHALL include the Access-Control-Allow-Credentials header with a value of true in CORS responses, so that the frontend can send the Authorization header in cross-origin requests.
6. WHEN the Orders_API receives a CORS preflight request from an allowed origin, THE Orders_API SHALL include an Access-Control-Max-Age header with a value of 600 seconds to reduce redundant preflight requests.

### Requirement 9: API Rate Limiting

**User Story:** As a developer using the template, I want rate limiting on the public API endpoints, so that the service is protected from abuse beyond just the MCP layer.

#### Acceptance Criteria

1. THE Orders_API SHALL apply a fixed-window rate limit of a configurable number of requests per minute (default: 100, minimum configurable value: 1, maximum configurable value: 10000) per authenticated user on the `/api/orders` endpoint group.
2. IF a client exceeds the rate limit, THEN THE Orders_API SHALL return HTTP 429 (Too Many Requests) with a `Retry-After` header containing the number of whole seconds remaining until the current fixed window resets.
3. THE rate limiting configuration SHALL be read from application configuration, exposing at minimum the permit limit per window and the window duration in seconds, to allow per-environment tuning.
4. IF a request to the `/api/orders` endpoint group cannot be associated with an authenticated user identity, THEN THE Orders_API SHALL apply the rate limit using the client IP address as the key.
5. WHEN a request to the `/api/orders` endpoint group is processed within the rate limit, THE Orders_API SHALL include `X-RateLimit-Limit` and `X-RateLimit-Remaining` response headers indicating the window permit limit and the number of remaining requests in the current window.

### Requirement 10: OpenAPI Documentation Endpoint

**User Story:** As a developer using the template, I want an OpenAPI specification endpoint exposed, so that API consumers can discover and understand available endpoints.

#### Acceptance Criteria

1. THE Orders_API SHALL expose an OpenAPI 3.0 specification document at `/openapi/v1.json` without requiring authentication, returning a response with Content-Type `application/json` and HTTP status 200.
2. THE OpenAPI document SHALL include all publicly routed endpoint definitions with request/response schemas, status codes, and summary descriptions.
3. WHILE the application is running in the Development environment, THE Orders_API SHALL serve a Swagger UI at `/swagger` that renders the OpenAPI specification for interactive API exploration.
4. IF a request is made to `/swagger` while the application is NOT running in the Development environment, THEN THE Orders_API SHALL return HTTP status 404.

### Requirement 11: Resilience for External HTTP Calls

**User Story:** As a developer using the template, I want the HTTP client resilience pipeline registered and demonstrated, so that developers see the pattern for calling external services with retry and circuit breaker.

#### Acceptance Criteria

1. THE Infrastructure_Layer SHALL register the InventoryHttpClient with the standard resilience handler providing retry (3 attempts, exponential backoff), circuit breaker (5 consecutive failures open for 30 seconds), and total request timeout (30 seconds).
2. THE Orders_API SHALL register the HTTP client resilience pipeline in the composition root (Program.cs) by calling the infrastructure extension method such that InventoryHttpClient is resolvable from the DI container at application startup.
3. WHEN the InventoryHttpClient encounters a transient HTTP failure (5xx, 408, or network error), THE Resilience_Pipeline SHALL retry the request up to 3 times with exponential backoff before surfacing the error to the caller.
4. IF all retry attempts configured in the resilience pipeline are exhausted and the downstream service remains unreachable, THEN THE Infrastructure_Layer SHALL throw a ServiceUnavailableException containing an error message indicating service unavailability, so the presentation layer can map it to an HTTP 503 response.
5. IF the total request timeout of 30 seconds elapses before a successful response is received, THEN THE Infrastructure_Layer SHALL throw a ServiceUnavailableException containing an error message indicating the request timed out, so the presentation layer can map it to an HTTP 503 response.

### Requirement 12: Clean Architecture Boundary Enforcement

**User Story:** As a developer using the template, I want comprehensive architecture tests, so that Clean Architecture dependency rules cannot be accidentally violated.

#### Acceptance Criteria

1. THE Architecture_Tests SHALL verify that the Domain layer has zero NuGet package dependencies beyond the .NET runtime (no external packages referenced in Orders.Domain.csproj).
2. THE Architecture_Tests SHALL verify that all classes in the Domain layer implementing any MediatR interface (`IRequest`, `IRequestHandler`, `INotification`, or `INotificationHandler`) produce a test failure, and the failure message SHALL list the fully qualified names of all violating types.
3. THE Architecture_Tests SHALL verify that no class in the Application layer has a dependency on the `Microsoft.EntityFrameworkCore` namespace or any of its sub-namespaces (including references to `DbContext` and `DbSet`).
4. THE Architecture_Tests SHALL verify that interfaces in the Domain layer whose name matches the pattern `I*Repository` are implemented only by classes in the Infrastructure layer, and the test failure message SHALL list any violating implementations found outside Infrastructure.
5. IF any architecture test detects a violation, THEN THE Architecture_Tests SHALL report the violating type's fully qualified name and the layer in which it was found.

### Requirement 13: Integration Testing Infrastructure

**User Story:** As a developer using the template, I want a WebApplicationFactory-based integration test setup, so that developers can write end-to-end tests against the API with a real database.

#### Acceptance Criteria

1. THE test infrastructure SHALL provide a custom `WebApplicationFactory<Program>` that replaces the production PostgreSQL connection with a Testcontainers-managed PostgreSQL container or an in-memory database for isolation.
2. THE test infrastructure SHALL reset the database state between test classes by re-applying migrations to an empty database, ensuring no test pollution from prior test classes.
3. THE test infrastructure SHALL include at least one integration test that exercises the full request pipeline: HTTP request → endpoint → MediatR handler → EF Core repository → database, asserting both the HTTP response and the persisted database state.
4. THE test infrastructure SHALL replace the MassTransit transport with the InMemory test harness so that published messages can be asserted in tests without requiring a running RabbitMQ instance.
5. THE test infrastructure SHALL bypass JWT authentication in integration tests by replacing the authentication scheme with a test handler that always succeeds.

### Requirement 14: Frontend Error Handling and Loading States

**User Story:** As a developer using the template, I want the frontend to demonstrate proper error handling and loading state patterns, so that the React SPA provides a production-quality user experience template.

#### Acceptance Criteria

1. WHEN the API returns an error response with a ProblemDetails body, THE frontend SHALL display the `detail` field value to the user, or the `title` field value if `detail` is absent.
2. IF the API returns a ProblemDetails body containing an `errors` dictionary, THEN THE frontend SHALL display at least the first validation error message associated with each field key listed in the dictionary.
3. IF an API request fails due to a network error or timeout (no HTTP response received), THEN THE frontend SHALL display an error message indicating that the server could not be reached.
4. WHILE an API request is in progress, THE frontend SHALL display a visible loading indicator within the UI component that initiated the request, and the indicator SHALL include an ARIA live region or appropriate `aria-busy` attribute for assistive technology.
5. WHEN the API returns HTTP 401, THE frontend SHALL clear the stored authentication token from the Zustand auth store and redirect the user to the `/login` route.
6. THE frontend SHALL include an error boundary component at the application root that catches unhandled React rendering errors and displays a fallback UI containing an error message and a recovery action that reloads the page or resets the component tree.
7. WHEN an error message or fallback UI is rendered, THE frontend SHALL use an element with `role="alert"` or an ARIA live region so that the error is announced to assistive technologies.

### Requirement 15: Frontend Environment Configuration

**User Story:** As a developer using the template, I want the frontend API base URL configurable per environment, so that the SPA works in both local development and deployed environments without code changes.

#### Acceptance Criteria

1. THE frontend SHALL read the API base URL from the Vite environment variable `VITE_API_BASE_URL` and use it as the HTTP client's baseURL for all API requests.
2. WHEN the `VITE_API_BASE_URL` environment variable is not defined at build time, THE frontend SHALL default the HTTP client's baseURL to `/api` (relative path for same-origin deployment).
3. THE Docker Compose nginx configuration SHALL proxy requests with the path prefix `/api` to the orders-api container on port 8080, preserving the remaining path segments.
4. THE frontend Dockerfile SHALL accept a build argument (`VITE_API_BASE_URL`) and pass it as an environment variable during the Vite build step, enabling Docker Compose to override the default at image build time.

### Requirement 16: CI Pipeline — EF Migrations Validation

**User Story:** As a developer using the template, I want CI to validate that EF migrations are up-to-date, so that schema drift between the model and migrations is caught before merge.

#### Acceptance Criteria

1. WHEN a pull request is opened or updated with new commits, THE CI pipeline SHALL run `dotnet ef migrations has-pending-model-changes` and fail the build with a non-zero exit code if the model has diverged from the latest migration.
2. WHEN the pending-model-changes check passes, THE CI pipeline SHALL run `dotnet ef database update` against a temporary PostgreSQL container starting from an empty database and fail the build if the command exits with a non-zero exit code.
3. IF the temporary PostgreSQL container does not become ready within 30 seconds, THEN THE CI pipeline SHALL fail the build with an error message indicating the database service was unavailable.

### Requirement 17: Docker Compose — Production-Equivalent Messaging

**User Story:** As a developer using the template, I want the Docker Compose MassTransit configuration to use the RabbitMQ transport, so that local development exercises the same messaging path as production.

#### Acceptance Criteria

1. WHEN running via Docker Compose, THE Orders_API SHALL configure MassTransit with the RabbitMQ transport connecting to the rabbitmq service container using the `RabbitMq__Host`, `RabbitMq__Username`, and `RabbitMq__Password` environment variables.
2. THE Docker Compose configuration SHALL map the RabbitMQ management UI port (15672) and the AMQP port (5672) for local message inspection and client connectivity.
3. WHEN running outside Docker Compose without a `RabbitMq__Host` configuration value, THE Orders_API SHALL fall back to the InMemory transport and log a warning at Warning level with the message "RabbitMQ host not configured; using InMemory transport (degraded mode)".

### Requirement 18: Outbox Table Retention and Monitoring

**User Story:** As a developer using the template, I want the outbox table to be self-managing, so that it does not grow unbounded in production.

#### Acceptance Criteria

1. THE Infrastructure_Layer SHALL include a background service that periodically (configurable interval, default: 60 minutes, minimum: 1 minute) deletes outbox messages where ProcessedAt is not null and ProcessedAt is older than the configured retention period (default: 7 days).
2. THE retention service SHALL log the count of deleted messages at Information level after each cleanup cycle, including the retention period used.
3. THE initial EF Core migration SHALL add an index on the `ProcessedAt` column of the outbox_messages table to optimize both the polling query (WHERE ProcessedAt IS NULL) and the retention cleanup query (WHERE ProcessedAt < @threshold).
4. THE retention service SHALL delete messages in batches of a configurable size (default: 500) to avoid long-running transactions that could block outbox processing.

### Requirement 19: Security — Input Validation at API Boundary

**User Story:** As a developer using the template, I want all API endpoints to demonstrate input validation at the HTTP boundary, so that invalid data is rejected before reaching domain logic.

#### Acceptance Criteria

1. WHEN a PlaceOrder request contains a Quantity value less than 1 on any order line, THE Orders_API SHALL return HTTP 400 with a ProblemDetails body whose "errors" dictionary includes an entry keyed by the property name containing a message indicating the validation failure.
2. WHEN a PlaceOrder request contains a UnitPrice value less than or equal to zero on any order line, THE Orders_API SHALL return HTTP 400 with a ProblemDetails body whose "errors" dictionary includes an entry keyed by the property name containing a message indicating the validation failure.
3. WHEN a PlaceOrder request contains an empty Lines collection, THE Orders_API SHALL return HTTP 400 with a ProblemDetails body whose "errors" dictionary includes an entry indicating at least one line is required.
4. WHEN any endpoint receives a request body exceeding 1 MB, THE Orders_API SHALL return HTTP 413 (Payload Too Large) without deserializing the body into a domain object.
5. IF any endpoint receives a request body that is not valid JSON, THEN THE Orders_API SHALL return HTTP 400 with a ProblemDetails body indicating a malformed request payload.
6. THE validation layer SHALL use FluentValidation validators registered in the MediatR pipeline as an IPipelineBehavior, ensuring all validation rules execute and fail before the command handler is invoked.

### Requirement 20: Security — HTTPS and Security Headers

**User Story:** As a developer using the template, I want security headers configured, so that the API follows OWASP security best practices for HTTP responses.

#### Acceptance Criteria

1. THE Orders_API SHALL include a `X-Content-Type-Options: nosniff` header on all HTTP responses regardless of status code or endpoint.
2. WHILE the Orders_API is serving requests over HTTPS, THE Orders_API SHALL include a `Strict-Transport-Security` header with a `max-age` value of at least 31536000 seconds and the `includeSubDomains` directive on all responses.
3. IF the Orders_API receives a request over plain HTTP, THEN THE Orders_API SHALL NOT include the `Strict-Transport-Security` header in the response.
4. THE Orders_API SHALL include a `X-Frame-Options: DENY` header on all HTTP responses regardless of status code or endpoint.
5. THE Orders_API SHALL NOT include a `Server` header in any HTTP response, such that the header is absent from the response rather than present with an empty value.

