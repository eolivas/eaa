# Implementation Plan: Template Architecture Gaps

## Overview

This plan implements 20 architectural gaps in the EAA .NET 8 Clean Architecture template. The implementation follows an incremental approach: infrastructure and cross-cutting concerns first, then domain/application layer enhancements, API pipeline assembly, frontend improvements, Docker Compose observability stack, CI pipeline additions, and finally architecture/integration tests to validate everything.

## Tasks

- [x] 1. Set up project structure, configuration models, and EF Core migrations infrastructure
  - [x] 1.1 Create configuration option classes (OutboxOptions, OutboxRetentionOptions, RateLimitOptions, RabbitMqOptions) in Orders.Infrastructure
    - Create `OutboxOptions` with BatchSize (default 20), MaxRetries (default 5), PollingIntervalSeconds (default 5)
    - Create `OutboxRetentionOptions` with IntervalMinutes (default 60), RetentionDays (default 7), BatchSize (default 500)
    - Create `RateLimitOptions` with PermitLimit (default 100), WindowSeconds (default 60)
    - Create `RabbitMqOptions` with Host, Username (default "guest"), Password (default "guest"), ConsumerRetryCount (default 3), StartupRetryAttempts (default 5)
    - _Requirements: 1.1, 1.2, 1.4, 2.1, 2.2, 2.3, 9.1, 9.3, 18.1, 18.4_

  - [x] 1.2 Add RetryCount, FailedAt, and FailureReason columns to OutboxMessage entity
    - Add `RetryCount` (int, default 0), `FailedAt` (DateTime?, nullable), `FailureReason` (string?, nullable) properties to OutboxMessage
    - Update EF entity configuration with column mappings and default values
    - _Requirements: 2.2, 2.3_

  - [x] 1.3 Create DesignTimeDbContextFactory for EF CLI tooling
    - Implement `IDesignTimeDbContextFactory<OrdersDbContext>` in Orders.Infrastructure with a hardcoded dev connection string
    - _Requirements: 3.5_

  - [x] 1.4 Generate initial EF Core migration
    - Run `dotnet ef migrations add InitialCreate` to produce migration for orders, order_lines, and outbox_messages tables
    - Include composite filtered index on outbox_messages (ProcessedAt WHERE FailedAt IS NULL) and retention index (ProcessedAt WHERE ProcessedAt IS NOT NULL)
    - _Requirements: 3.1, 18.3_

  - [x] 1.5 Implement auto-migration and environment-conditional startup behavior
    - In Development: call `dbContext.Database.MigrateAsync()` before `app.Run()`; terminate on failure
    - In non-Development: call `GetPendingMigrationsAsync()`, log warning count, continue
    - _Requirements: 3.2, 3.3, 3.4_

- [x] 2. Implement MassTransit RabbitMQ transport and messaging infrastructure
  - [x] 2.1 Create MassTransit RabbitMQ/InMemory conditional registration extension method
    - Implement `AddMessaging(IServiceCollection, IConfiguration)` extension
    - If `RabbitMq:Host` present: configure `UsingRabbitMq` with retry policy (configurable retries, exponential 1s→8s), error queue with `_error` suffix
    - If `RabbitMq:Host` absent: use `UsingInMemory`, log warning "RabbitMQ host not configured; using InMemory transport (degraded mode)"
    - Configure startup retry with exponential backoff (5 attempts, 1s initial doubling)
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 17.1, 17.3_

  - [x] 2.2 Write unit tests for MassTransit conditional registration
    - Test RabbitMq:Host present path uses RabbitMQ transport
    - Test RabbitMq:Host absent path uses InMemory transport and logs warning
    - _Requirements: 1.1, 17.3_

- [x] 3. Implement outbox processor hardening and retention service
  - [x] 3.1 Enhance OutboxProcessor with batch processing, retry counting, and dead-lettering
    - Read batch size from OutboxOptions configuration
    - Query with `.Where(m => m.ProcessedAt == null && m.FailedAt == null).OrderBy(m => m.OccurredAt).Take(batchSize)`
    - On failure: increment RetryCount; if >= maxRetries, set FailedAt and FailureReason
    - Add OTEL metrics: `outbox.messages.processed` counter, `outbox.messages.failed` counter, `outbox.message.duration_ms` histogram
    - _Requirements: 2.1, 2.2, 2.3, 2.5_

  - [x] 3.2 Write property test for outbox batch ordering and size (Property 1)
    - **Property 1: Outbox Batch Ordering and Size**
    - Generate arbitrary sets of unprocessed outbox messages with distinct OccurredAt timestamps
    - Assert retrieval returns at most batchSize messages ordered by OccurredAt ascending
    - Use FsCheck with in-memory DbContext
    - **Validates: Requirements 2.1**

  - [x] 3.3 Write property test for outbox retry state machine (Property 2)
    - **Property 2: Outbox Retry State Machine**
    - Generate arbitrary outbox messages with varying RetryCount values relative to configured maximum
    - Assert: if RetryCount < max, it increments and message stays eligible; if RetryCount >= max, FailedAt is set and message excluded
    - Use FsCheck with in-memory DbContext
    - **Validates: Requirements 2.2, 2.3**

  - [x] 3.4 Implement OutboxRetentionService background service
    - Run on configurable interval (default 60 min) from OutboxRetentionOptions
    - Delete messages where ProcessedAt != null AND ProcessedAt older than retention period
    - Delete in batches of configurable size (default 500)
    - Log count of deleted messages at Information level
    - _Requirements: 2.4, 18.1, 18.2, 18.4_

  - [x] 3.5 Write property test for outbox retention cleanup (Property 3)
    - **Property 3: Outbox Retention Cleanup**
    - Generate arbitrary sets of outbox messages with varying ProcessedAt timestamps (some within retention, some expired)
    - Assert exactly the expired messages are deleted, in batches of configured size, and non-expired messages remain
    - Use FsCheck with in-memory DbContext
    - **Validates: Requirements 2.4, 18.1, 18.4**

- [x] 4. Checkpoint - Ensure outbox and messaging tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Implement API middleware pipeline (Correlation ID, Security Headers, CORS, Rate Limiting)
  - [x] 5.1 Implement CorrelationIdMiddleware
    - Extract `X-Correlation-Id` header; validate is non-empty valid GUID
    - If valid: use as-is; if invalid/missing: generate new GUID
    - Push `CorrelationId` to Serilog LogContext for request duration
    - Set `X-Correlation-Id` response header via `OnStarting` callback
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 5.2 Write property test for correlation ID round-trip (Property 4)
    - **Property 4: Correlation ID Round-Trip**
    - Generate arbitrary strings (valid GUIDs, empty, non-GUID): assert valid GUIDs echo back, invalid/empty produce new valid GUID
    - Test middleware with DefaultHttpContext
    - **Validates: Requirements 7.1, 7.2, 7.3, 7.4**

  - [x] 5.3 Implement SecurityHeadersMiddleware
    - Add `X-Content-Type-Options: nosniff` on every response
    - Add `X-Frame-Options: DENY` on every response
    - Remove `Server` header on every response
    - Add `Strict-Transport-Security: max-age=31536000; includeSubDomains` only when `context.Request.IsHttps`
    - _Requirements: 20.1, 20.2, 20.3, 20.4, 20.5_

  - [x] 5.4 Write property test for security headers (Property 11)
    - **Property 11: Security Headers Present on All Responses**
    - Generate arbitrary HTTP methods, status codes, and endpoints
    - Assert X-Content-Type-Options: nosniff and X-Frame-Options: DENY are always present, Server header always absent
    - **Validates: Requirements 20.1, 20.4, 20.5**

  - [x] 5.5 Write property test for HSTS conditional on transport (Property 12)
    - **Property 12: HSTS Conditional on Transport**
    - Generate arbitrary requests over HTTPS and HTTP
    - Assert HSTS present on HTTPS with correct max-age and includeSubDomains; absent on HTTP
    - **Validates: Requirements 20.2, 20.3**

  - [x] 5.6 Implement CORS configuration via extension method
    - Read allowed origins from `Cors:AllowedOrigins` configuration array
    - Allow methods: GET, POST, PUT, DELETE, OPTIONS
    - Allow headers: Authorization, Content-Type, X-Correlation-Id
    - AllowCredentials, SetPreflightMaxAge(600 seconds)
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6_

  - [x] 5.7 Write property test for CORS rejection (Property 5)
    - **Property 5: CORS Rejection for Non-Allowed Origins**
    - Generate arbitrary Origin header values not in configured allowed list
    - Assert response does NOT contain Access-Control-Allow-Origin, Access-Control-Allow-Methods, or Access-Control-Allow-Headers
    - **Validates: Requirements 8.2**

  - [x] 5.8 Implement rate limiting with fixed window and response headers
    - Register `AddRateLimiter` with fixed window policy "orders-api" reading from RateLimitOptions
    - Partition by authenticated user `sub` claim or fallback to RemoteIpAddress
    - On rejection: return 429 with Retry-After header (seconds remaining in window)
    - On success: include X-RateLimit-Limit and X-RateLimit-Remaining headers
    - Apply to `/api/orders` endpoint group via `.RequireRateLimiting("orders-api")`
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

  - [x] 5.9 Write property test for rate limit enforcement (Property 6)
    - **Property 6: Rate Limit Enforcement**
    - Generate arbitrary permit limits and request counts exceeding the limit
    - Assert the (N+1)th request returns 429 with Retry-After containing positive integer
    - **Validates: Requirements 9.1, 9.2**

  - [x] 5.10 Write property test for rate limit response headers (Property 7)
    - **Property 7: Rate Limit Response Headers**
    - Generate arbitrary request sequences within the limit
    - Assert X-RateLimit-Limit equals configured permit limit, X-RateLimit-Remaining equals permitLimit - requestCount
    - **Validates: Requirements 9.5**

- [x] 6. Implement input validation and request size limiting
  - [x] 6.1 Create PlaceOrderCommandValidator using FluentValidation
    - Validate `Lines` not empty (rule: must have at least one line)
    - Validate each line: Quantity >= 1, UnitPrice > 0
    - Register validator as IPipelineBehavior in MediatR pipeline
    - _Requirements: 19.1, 19.2, 19.3, 19.6_

  - [x] 6.2 Write property test for input validation numeric constraints (Property 8)
    - **Property 8: Input Validation — Order Line Numeric Constraints**
    - Generate arbitrary order lines with Quantity < 1 or UnitPrice <= 0
    - Assert API returns 400 with ProblemDetails errors keyed by property name
    - **Validates: Requirements 19.1, 19.2**

  - [x] 6.3 Implement request body size limit (1 MB) middleware
    - Reject requests with Content-Length > 1,048,576 bytes with HTTP 413 without deserialization
    - Configure Kestrel MaxRequestBodySize or use inline middleware
    - _Requirements: 19.4_

  - [x] 6.4 Write property test for oversized payload rejection (Property 9)
    - **Property 9: Oversized Payload Rejection**
    - Generate arbitrary Content-Length values > 1,048,576
    - Assert API returns 413 without body deserialization
    - **Validates: Requirements 19.4**

  - [x] 6.5 Write property test for malformed JSON rejection (Property 10)
    - **Property 10: Malformed JSON Rejection**
    - Generate arbitrary non-JSON byte sequences (random bytes, truncated JSON, XML, plain text)
    - Assert API returns 400 with ProblemDetails indicating malformed request
    - **Validates: Requirements 19.5**

- [x] 7. Checkpoint - Ensure middleware and validation tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 8. Implement health checks, OpenAPI, and observability enhancements
  - [x] 8.1 Register health checks for PostgreSQL and RabbitMQ
    - Add `AddHealthChecks().AddNpgSql(...)` and `.AddRabbitMQ(...)` with 5s timeout and "ready" tags
    - Map `GET /health/live` (liveness, always healthy) and `GET /health/ready` (readiness, "ready" tag filter)
    - Return JSON response writer with entries; no auth required (AllowAnonymous)
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

  - [x] 8.2 Register OpenAPI and conditional Swagger UI
    - Call `builder.Services.AddOpenApi()` for `/openapi/v1.json` endpoint (no auth)
    - Conditionally add `app.UseSwaggerUI()` only in Development environment
    - Ensure 404 returned for `/swagger` in non-Development
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

  - [x] 8.3 Enhance OpenTelemetry registration with WithMetrics
    - Add `.WithMetrics(mp => mp.AddAspNetCoreInstrumentation().AddMeter("Orders.Mcp").AddOtlpExporter())` alongside existing WithTracing
    - Configure OTLP exporter endpoint from configuration
    - Ensure non-fatal behavior if collector is unreachable
    - _Requirements: 6.1, 6.5, 6.7_

  - [x] 8.4 Register HTTP client resilience pipeline in Program.cs
    - Call `AddInventoryHttpClient(builder.Configuration)` extension method in composition root
    - Verify InventoryHttpClient is resolvable from DI container
    - _Requirements: 11.1, 11.2_

- [x] 9. Implement database seeding for Development environment
  - [x] 9.1 Create DatabaseSeeder static class
    - Check orders table has 0 records; skip if non-empty
    - Create 4 orders: Pending (Create), Placed (Create→Place), Cancelled (Create→Cancel), Shipped (direct status set)
    - Each order has 1-3 order lines with realistic amounts
    - Only execute in Development environment
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

- [x] 10. Wire middleware pipeline in Program.cs (correct ordering)
  - [x] 10.1 Assemble the complete middleware pipeline in correct order
    - Order: SecurityHeaders → CorrelationId → ExceptionHandler → CORS → RateLimiting → Authentication → Authorization → Endpoint Routing
    - Register all services: CORS, rate limiter, health checks, OpenAPI, MassTransit messaging, FluentValidation
    - Wire migration/seeding startup logic
    - _Requirements: 7.4, 8.1, 9.1, 20.1_

- [x] 11. Implement Correlation ID propagation in outbox and consumers
  - [x] 11.1 Add correlation ID to outbox message publishing and MassTransit consumers
    - Outbox processor: include CorrelationId in MassTransit headers from outbox message context
    - MassTransit consumers: extract X-Correlation-Id header and push to Serilog LogContext
    - _Requirements: 7.6, 7.7_

- [x] 12. Checkpoint - Ensure all backend tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 13. Implement frontend improvements (error handling, environment config)
  - [x] 13.1 Create ErrorBoundary class component at app root
    - Class component wrapping children; catches rendering errors
    - Renders fallback with `role="alert"` and reload button
    - _Requirements: 14.6, 14.7_

  - [x] 13.2 Implement useApiError hook for ProblemDetails parsing
    - Parse Axios error responses into ProblemDetails shape (title, detail, errors dict)
    - Display `detail` or fallback to `title`
    - Per-field validation errors from `errors` dictionary
    - _Requirements: 14.1, 14.2_

  - [x] 13.3 Implement loading states with aria-busy and network/401 error handling
    - Add loading indicator with `aria-busy="true"` on containing element
    - 401 handler: clear Zustand auth store, redirect to `/login`
    - Network error detection: display "Unable to reach the server" message
    - _Requirements: 14.3, 14.4, 14.5_

  - [x] 13.4 Write property test for ProblemDetails field error display (Property 13)
    - **Property 13: ProblemDetails Field Error Display**
    - Generate arbitrary ProblemDetails responses with random field keys and error arrays (N ≥ 1)
    - Assert frontend renders at least the first validation error for each field key
    - Use fast-check with Vitest
    - **Validates: Requirements 14.2**

  - [x] 13.5 Configure environment-based API URL in http.ts
    - Use `import.meta.env.VITE_API_BASE_URL || '/api'` as baseURL
    - Update frontend Dockerfile to accept `ARG VITE_API_BASE_URL` and pass as ENV during build
    - _Requirements: 15.1, 15.2, 15.4_

- [x] 14. Update Docker Compose with observability stack and nginx proxy
  - [x] 14.1 Add OTEL Collector, Jaeger, and Prometheus services to docker-compose.yml
    - Add `otel-collector` (otel/opentelemetry-collector-contrib:0.96.0) with config for Jaeger traces + Prometheus metrics export
    - Add `jaeger` (jaegertracing/all-in-one:1.54) on port 16686
    - Add `prometheus` (prom/prometheus:v2.50.0) on port 9090 with scrape config
    - Add `depends_on: otel-collector: condition: service_healthy` to orders-api
    - _Requirements: 6.2, 6.3, 6.4, 6.6_

  - [x] 14.2 Configure nginx to proxy /api to orders-api and expose RabbitMQ ports
    - Add `location /api/ { proxy_pass http://orders-api:8080/api/; }` to nginx.conf
    - Ensure RabbitMQ management (15672) and AMQP (5672) ports are mapped
    - _Requirements: 15.3, 17.2_

- [x] 15. Implement CI pipeline EF migrations validation
  - [x] 15.1 Add ef-migrations-check job to ci.yml workflow
    - Add PostgreSQL service container with health check (pg_isready, 30s timeout)
    - Install dotnet-ef tool
    - Run `dotnet ef migrations has-pending-model-changes` — fail build on divergence
    - Run `dotnet ef database update` against temp PG — fail build on error
    - _Requirements: 16.1, 16.2, 16.3_

- [x] 16. Create architecture tests project (Orders.Architecture.Tests)
  - [x] 16.1 Create Orders.Architecture.Tests xUnit project with NetArchTest.Rules
    - Add project to solution with references to Domain, Application, Infrastructure
    - Install NetArchTest.Rules NuGet package
    - _Requirements: 12.1_

  - [x] 16.2 Implement architecture boundary test cases
    - Test: Domain project has zero PackageReference items (parse .csproj XML)
    - Test: No type in Domain implements MediatR interfaces (IRequest, IRequestHandler, INotification, INotificationHandler)
    - Test: No type in Application references Microsoft.EntityFrameworkCore namespace
    - Test: I*Repository interfaces in Domain implemented only in Infrastructure
    - All failure messages include fully qualified type names
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_

- [x] 17. Create integration tests project (Orders.Integration.Tests)
  - [x] 17.1 Create Orders.Integration.Tests xUnit project with WebApplicationFactory and Testcontainers
    - Add project to solution
    - Install Testcontainers.PostgreSql, Microsoft.AspNetCore.Mvc.Testing packages
    - Create `OrdersWebApplicationFactory` with Testcontainers PostgreSQL, InMemory MassTransit, test auth handler
    - Implement database reset between test classes (re-migration)
    - _Requirements: 13.1, 13.2, 13.4, 13.5_

  - [x] 17.2 Implement end-to-end integration test for order placement
    - POST `/api/orders` with valid payload → verify 201 + DB row + outbox message
    - Assert MassTransit InMemoryTestHarness receives published message
    - _Requirements: 13.3_

- [x] 18. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document (FsCheck for C#, fast-check for TypeScript)
- Unit tests validate specific examples and edge cases
- Architecture tests enforce Clean Architecture dependency rules at build time
- Integration tests exercise the full request pipeline with real database containers

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.3", "16.1"] },
    { "id": 1, "tasks": ["1.2", "1.4", "5.1", "5.3", "13.1", "13.5"] },
    { "id": 2, "tasks": ["1.5", "2.1", "5.2", "5.4", "5.5", "5.6", "13.2"] },
    { "id": 3, "tasks": ["2.2", "3.1", "5.7", "5.8", "6.1", "13.3"] },
    { "id": 4, "tasks": ["3.2", "3.3", "3.4", "5.9", "5.10", "6.2", "6.3", "13.4"] },
    { "id": 5, "tasks": ["3.5", "6.4", "6.5", "8.1", "8.2", "8.3", "8.4"] },
    { "id": 6, "tasks": ["9.1", "10.1", "11.1", "14.1", "14.2"] },
    { "id": 7, "tasks": ["15.1", "16.2", "17.1"] },
    { "id": 8, "tasks": ["17.2"] }
  ]
}
```
