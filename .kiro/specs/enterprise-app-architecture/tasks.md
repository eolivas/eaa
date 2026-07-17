# Implementation Plan: Enterprise Application Architecture

## Overview

This plan implements the EAA reference repository in foundational-first order: Domain layer first (zero dependencies), then Application layer, then Infrastructure, then Presentation/API, then cross-cutting concerns (Docker, CI/CD, observability, docs). All code uses C# 12 / .NET 8. The Orders/Money domain is used as the illustrative PoC throughout.

---

## Tasks

- [x] 1. Scaffold solution structure and project skeleton
  - Create `Orders.sln` with four class-library/web-api projects: `Orders.Domain`, `Orders.Application`, `Orders.Infrastructure`, `Orders.Api`
  - Create five test projects: `Orders.Domain.Tests`, `Orders.Application.Tests`, `Orders.Infrastructure.Tests`, `Orders.Api.Tests`, `Orders.Architecture.Tests`
  - Configure project references to enforce Clean Architecture dependency direction (Domain ← Application ← Infrastructure ← Api; test projects reference their target layer)
  - Add `Directory.Build.props` enabling `TreatWarningsAsErrors`, nullable reference types, and implicit usings
  - Add `global.json` pinning .NET 8 SDK
  - Add `Orders.Domain.csproj` with zero external NuGet dependencies
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 2. Implement Domain layer — primitives and building blocks
  - [x] 2.1 Create domain primitives: `Entity<TId>`, `AggregateRoot<TId>`, `DomainEvent` base record, `OrderDomainException`
    - `AggregateRoot<TId>` holds `List<DomainEvent>` with `RaiseDomainEvent` and `ClearDomainEvents`
    - `OrderDomainException` extends `Exception` (used by HTTP 422 mapping)
    - _Requirements: 3.3, 5.7_

  - [x] 2.2 Implement `Money` value object
    - Immutable C# `record`; throws `ArgumentException` for negative amounts and non-ISO-4217 currency codes
    - Implement `+` operator, `*` operators, `Zero` factory, `ToString`
    - _Requirements: 3.2_

  - [ ]* 2.3 Write property tests for `Money` (Property 4, Property 5)
    - **Property 4: Money rejects invalid construction arguments** — `Amount < 0` or currency not three uppercase letters always throws `ArgumentException`
    - **Property 5: Money addition is commutative and non-negative for non-negative inputs** — `(a + b).Amount == (b + a).Amount` and result ≥ 0
    - **Validates: Requirements 3.2, 9.3**

  - [x] 2.4 Implement `OrderStatus` enum and `OrderId` / `CustomerId` / `ProductId` / `OrderLineId` strongly-typed ID wrappers
    - _Requirements: 3.8, 10.1_

  - [x] 2.5 Implement `OrderLine` entity with `Create` static factory
    - `Create` throws `OrderDomainException` when `qty <= 0`
    - Exposes `LineTotal` computed property
    - _Requirements: 6.2_

  - [x] 2.6 Implement `Order` aggregate root with full status lifecycle
    - `Create` factory: throws `OrderDomainException` when `lines` is empty; raises `OrderCreatedEvent`
    - `Place()`: throws when `Status != Pending`; transitions to `Placed`; raises `OrderPlacedEvent`
    - `Cancel(reason)`: throws when `Status` is `Shipped` or `Cancelled`; transitions to `Cancelled`; raises `OrderCancelledEvent`
    - `Total` computed as sum of `LineTotal` across all `OrderLine` entries
    - _Requirements: 3.1, 3.3, 3.5, 3.6, 3.8_

  - [ ]* 2.7 Write property tests for `Order` aggregate (Property 2, Property 3, Property 6)
    - **Property 2: Order creation with empty lines always throws** — any customerId + empty lines → `OrderDomainException`
    - **Property 3: Order total is non-negative and equals sum of line totals** — `order.Total.Amount ≥ 0`; total == sum of `UnitPrice × Quantity`
    - **Property 6: Order status transitions enforce the defined lifecycle** — invalid transitions always throw; valid transitions always succeed
    - **Validates: Requirements 3.1, 3.2, 3.5, 3.6, 3.8**

  - [x] 2.8 Implement `IOrderRepository` interface in Domain layer
    - Methods: `GetByIdAsync`, `GetByCustomerAsync`, `SaveAsync`, `DeleteAsync`
    - _Requirements: 3.4_

  - [x] 2.9 Implement domain event types: `OrderCreatedEvent`, `OrderPlacedEvent`, `OrderCancelledEvent`
    - _Requirements: 3.3_

- [x] 3. Implement Domain unit tests
  - [x] 3.1 Write `Order` aggregate unit tests (xUnit, no test doubles)
    - Cover: `Create` happy path, `Create` with empty lines throws, `Place` happy path, `Place` on non-Pending throws, `Cancel` happy path (Pending → Cancelled, Placed → Cancelled), `Cancel` on Shipped throws, `Cancel` on Cancelled throws
    - _Requirements: 9.1_

  - [x] 3.2 Write `OrderFaker` test-data builder using Bogus
    - Generates `Order` aggregates in both `Pending` and `Placed` states
    - Invariants: non-null `OrderId`, at least one `OrderLine`, `Total.Amount ≥ 0`
    - _Requirements: 9.5_

- [x] 4. Implement SOLID principle PoC classes in Application / Domain
  - [x] 4.1 Implement ISP interfaces: `IOrderWriter`, `IOrderReader`, `IOrderExporter`
    - `IOrderWriter`: `PlaceOrder`, `CancelOrder`; `IOrderReader`: `GetOrder`; `IOrderExporter`: `ExportToPdf`
    - No method appears in more than one interface
    - _Requirements: 2.4_

  - [x] 4.2 Implement OCP / Strategy: `IDiscountStrategy`, `SeasonalDiscountStrategy`, `LoyaltyDiscountStrategy`, `PricingService`
    - `PricingService.Calculate` applies each strategy sequentially via `Aggregate`
    - Result is always ≤ input base price
    - _Requirements: 2.2, 6.5_

  - [ ]* 4.3 Write property test for discount strategies (Property 13)
    - **Property 13: Discount strategies never inflate the input price** — result of `PricingService.Calculate` always ≤ input price for any combination of strategies
    - **Validates: Requirements 6.5**

  - [x] 4.4 Add `IApplicationEventPublisher` abstraction in Application layer
    - `PublishAsync(DomainEvent, CancellationToken)` method
    - Used by `PlaceOrderHandler` and wired to `MassTransitEventPublisher` in Infrastructure
    - _Requirements: 2.1, 2.5_

- [x] 5. Implement Application layer — CQRS handlers and pipeline behaviours
  - [x] 5.1 Implement `PlaceOrderCommand`, `PlaceOrderHandler`, and `PlaceOrderCommandValidator`
    - `PlaceOrderHandler` calls `Order.Create`, `order.Place()`, `_repo.SaveAsync`, then publishes domain events via `IApplicationEventPublisher`
    - `PlaceOrderCommandValidator` (FluentValidation): `Lines` must not be empty
    - `PlaceOrderHandler` creates `"PlaceOrder"` span via `ActivitySource.StartActivity` and sets `customer.id` tag
    - _Requirements: 4.1, 2.1, 16.3_

  - [ ]* 5.2 Write property test for `PlaceOrderHandler` (Property 18)
    - **Property 18: PlaceOrderHandler always calls SaveAsync and PublishAsync exactly once per valid command**
    - Moq `Times.Once()` assertions on both `_repo.SaveAsync` and `_publisher.PublishAsync`
    - **Validates: Requirements 9.2**

  - [x] 5.3 Implement `CancelOrderCommand` and `CancelOrderHandler`
    - Handler retrieves the order, calls `order.Cancel(reason)`, saves; throws `OrderDomainException` if order not found
    - _Requirements: 4.1_

  - [x] 5.4 Implement `GetOrderQuery`, `GetOrderHandler`, and `OrderDto` / `OrderLineDto`
    - Returns `OrderDto.From(order)` or `null` if not found
    - _Requirements: 4.2_

  - [x] 5.5 Implement `LoggingBehaviour<TRequest, TResponse>` pipeline behaviour
    - Logs `Information` before dispatch and after response; registers as outermost behaviour
    - _Requirements: 4.4, 4.7_

  - [x] 5.6 Implement `ValidationBehaviour<TRequest, TResponse>` pipeline behaviour
    - Runs all `IValidator<TRequest>` registered; throws `FluentValidation.ValidationException` if any failures
    - Registers as innermost behaviour (inside Logging, before handler)
    - _Requirements: 4.3, 4.7_

  - [ ]* 5.7 Write property test for `ValidationBehaviour` (Property 7)
    - **Property 7: ValidationBehaviour always prevents handler execution when validators fail** — any request with failing validators always throws `ValidationException` before next pipeline step
    - **Validates: Requirements 4.3, 4.5**

  - [x] 5.8 Write `PlaceOrderHandler` application tests (Moq)
    - Test: valid command → `SaveAsync` once, `PublishAsync` once
    - Test: empty `Lines` → `ValidationBehaviour` throws `ValidationException`, `SaveAsync` never called
    - _Requirements: 4.5, 9.2_

- [x] 6. Checkpoint — Domain and Application layers compile and tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Implement EF Core Infrastructure — persistence configuration
  - [x] 7.1 Create `OrdersDbContext` with `Orders` and `OutboxMessages` `DbSet` properties
    - Override `SaveChangesAsync` to intercept domain events, serialise them to `OutboxMessage` rows in the same transaction, then clear domain events post-save
    - _Requirements: 6.3, 10.5_

  - [x] 7.2 Implement `OrderEntityTypeConfiguration`
    - Maps `Order` to `orders` table; strongly-typed ID conversions for `OrderId` and `CustomerId`
    - Maps `Money` as owned entity (no separate join)
    - Maps `OrderLine` collection as owned-many in `order_lines` table with orphan deletion and `PropertyAccessMode.Field` targeting `_lines`
    - _Requirements: 10.1, 10.2, 10.3, 10.4_

  - [x] 7.3 Implement `OutboxMessage` entity and `OutboxMessageEntityTypeConfiguration`
    - Maps to `outbox_messages` table; `Id`, `EventType`, `Payload`, `OccurredAt` non-nullable; `ProcessedAt` nullable
    - _Requirements: 10.5_

  - [ ]* 7.4 Write property test for EF Core round-trip (Property 19, Property 20)
    - **Property 19: EF Core round-trip preserves Order, Money, and OrderLine values** — persist and retrieve returns equal `Id`, `CustomerId`, `Money.Amount`, `Money.Currency`, same `OrderLine` entries; orphan deletion removes line row
    - **Property 20: Unprocessed OutboxMessages always have ProcessedAt IS NULL** — unprocessed rows have `ProcessedAt IS NULL`; processed rows have non-null UTC timestamp
    - **Validates: Requirements 10.1, 10.2, 10.3, 10.5**

  - [x] 7.5 Implement `EfOrderRepository` with `.Include(o => o.Lines)` on every query
    - Implements `GetByIdAsync`, `GetByCustomerAsync`, `SaveAsync`, `DeleteAsync`
    - `SaveAsync` detects detached state and adds vs updates appropriately
    - Uses parameterised EF Core queries only (no raw SQL string concatenation)
    - _Requirements: 6.1, 18.4_

  - [ ]* 7.6 Write property test for `EfOrderRepository` (Property 11)
    - **Property 11: EfOrderRepository always returns Orders with Lines populated** — `order.Lines` is never null or empty for any retrieved order that was saved with lines
    - **Validates: Requirements 6.1**

- [x] 8. Implement Infrastructure layer — messaging, caching, resilience, and patterns
  - [x] 8.1 Implement `OutboxProcessor` background service
    - Polls `outbox_messages` every 5 seconds; deserialises events; publishes via MassTransit `IPublishEndpoint`; marks `ProcessedAt = UtcNow` in same transaction as publish acknowledgement
    - On exception: rolls back, logs at `Error` level with `OutboxMessage.Id`, leaves `ProcessedAt` null for retry
    - _Requirements: 6.3, 6.7_

  - [x] 8.2 Implement `MassTransitEventPublisher` implementing `IApplicationEventPublisher`
    - Delegates to `IPublishEndpoint.Publish`
    - _Requirements: 11.2_

  - [x] 8.3 Implement `OrderPlacedConsumer` for Notifications service
    - Implements `IConsumer<OrderPlacedEvent>`; stub body logs receipt of event (PoC)
    - _Requirements: 11.3_

  - [x] 8.4 Implement `CachedOrderRepository` decorator
    - Wraps `IOrderRepository` with `IDistributedCache`; cache miss → inner repo → store 5-min absolute expiry; cache hit → return cached without calling inner repo
    - _Requirements: 6.4_

  - [ ]* 8.5 Write property test for `CachedOrderRepository` (Property 12)
    - **Property 12: Cached repository hit never calls the inner repository** — second call for same ID hits cache; inner `IOrderRepository` is never called
    - **Validates: Requirements 6.4**

  - [x] 8.6 Implement `InventoryHttpClient` typed HTTP client with `AddStandardResilienceHandler`
    - Registered via `AddHttpClient<InventoryHttpClient>`; resilience handler covers retry and circuit-breaker
    - Returns HTTP 503 when all retries exhausted (unavailable)
    - _Requirements: 11.1, 11.4, 11.5_

  - [ ]* 8.7 Write property test for resilience layer (Property 21)
    - **Property 21: Resilience layer always returns HTTP 503 when downstream is unavailable after retries** — exhausted retries on any outbound call always produce HTTP 503 with service-unavailability message
    - **Validates: Requirements 11.4, 11.5**

  - [x] 8.8 Implement `Specification<T>` abstract base class and `PendingOrdersSpecification`
    - `ToExpression()` returns `Expression<Func<Order, bool>>` matching `Status == OrderStatus.Pending`
    - _Requirements: 6.6_

  - [ ]* 8.9 Write property test for `PendingOrdersSpecification` (Property 14)
    - **Property 14: PendingOrdersSpecification matches exactly the set of Pending orders** — expression evaluates to `true` iff `Status == OrderStatus.Pending`
    - **Validates: Requirements 6.6**

- [x] 9. Implement Presentation layer — Minimal API endpoints and middleware
  - [x] 9.1 Implement `ExceptionHandlingMiddleware`
    - Catches `OrderDomainException` → HTTP 422 ProblemDetails with exception message in `detail`; logs `Warning`
    - Catches `FluentValidation.ValidationException` → HTTP 400 ProblemDetails with field-keyed `errors` dictionary
    - Catches unhandled exceptions → HTTP 500 ProblemDetails; logs `Critical` with full stack trace
    - _Requirements: 5.7, 5.8_

  - [ ]* 9.2 Write property tests for `ExceptionHandlingMiddleware` (Property 8, Property 9, Property 10)
    - **Property 8: HTTP 400 always includes the failing field key** — any request failing FluentValidation on a named field always returns `errors` dict with that field key
    - **Property 9: DomainException always maps to HTTP 422 with exception message** — any `DomainException` caught by middleware always returns 422 + `detail` == exception message
    - **Property 10: Unhandled exceptions always produce HTTP 500 with Critical log** — any unhandled exception always returns 500 and logs Critical with stack trace
    - **Validates: Requirements 5.6, 5.7, 5.8**

  - [x] 9.3 Implement `MapOrdersEndpoints` using `MapGroup` + extension-method pattern
    - `POST /api/orders` → 201 + Location header; `GET /api/orders/{id}` → 200/404; `DELETE /api/orders/{id}` → 204/409
    - Annotate with `WithOpenApi()`, `WithSummary(string)`, `Produces<T>(int)` on every endpoint
    - Apply `RequireAuthorization()` to the endpoint group
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 18.1_

  - [ ]* 9.4 Write API endpoint unit tests
    - Test: `POST /api/orders` with valid body → 201 with Location header
    - Test: `POST /api/orders` with empty lines → 400 with field-keyed ProblemDetails
    - Test: `GET /api/orders/{id}` existing → 200 OrderDto; missing → 404
    - Test: `DELETE /api/orders/{id}` placed order → 409 Conflict
    - _Requirements: 5.1, 5.2, 5.3, 5.6_

  - [x] 9.5 Implement `Program.cs` bootstrap
    - Register MediatR with `LoggingBehaviour` (outermost) and `ValidationBehaviour` (innermost before handler)
    - Register `IOrderRepository` → `EfOrderRepository`, `IApplicationEventPublisher` → `MassTransitEventPublisher`
    - Register JWT Bearer authentication sourcing `Authority` and `Audience` from `IConfiguration` (no hardcoded string literals)
    - Register Serilog with JSON console, OpenTelemetry sink, `Service` log property from host configuration
    - Configure OpenTelemetry with ASP.NET Core and EF Core instrumentation exported via OTLP
    - Register MCP server with `WithHttpTransport().WithTools<OrderMcpTools>()`
    - _Requirements: 2.5, 4.6, 4.7, 16.1, 16.2, 16.4, 18.2_

- [ ] 10. Implement MCP Gateway tools and cost/context management
  - [x] 10.1 Implement `OrderMcpTools` class with `get_order` and `place_order` tools
    - `get_order`: returns serialised `OrderDto` or `"No order found with ID {orderId}."` on miss
    - `place_order`: deserialises `linesJson`, dispatches `PlaceOrderCommand`, returns `"Order placed successfully. Order ID: {id}"`; catches `DomainException` → returns exception message (never HTTP 500)
    - `place_order`: catches JSON parse/schema errors → returns descriptive error without stack trace
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.7_

  - [ ]* 10.2 Write property tests for MCP tools (Property 15, Property 16, Property 17)
    - **Property 15: get_order always returns not-found message for unknown IDs** — any unknown UUID → `CallToolResult.content[0].text` == `"No order found with ID {orderId}."`
    - **Property 16: MCP tools with invalid input always return descriptive error without stack trace** — any invalid `linesJson` → result describes validation error, never exposes stack trace
    - **Property 17: MCP DomainException always surfaces as user-readable message** — any `DomainException` → `CallToolResult.content[0].text` == exception message, not HTTP 500
    - **Validates: Requirements 7.3, 7.5, 7.7**

  - [x] 10.3 Create MCP tool manifest JSON file (`mcp-tools.json`)
    - Entries for `get_order` and `place_order` with `name`, `description`, `inputSchema` (type, properties, required)
    - _Requirements: 7.6_

  - [x] 10.4 Implement model-tier selection and context budget enforcement
    - Configurable tier map (Lightweight / Standard / Heavy); defaults to Lightweight when no mapping exists
    - Per-tool-call budget: 500 system prompt + 500 tool schemas + 2,000 history (sliding window) + 4,000 result + 1,000 margin = 8,000 tokens total
    - Truncate result payload at budget boundary; append `"\n[...truncated: result exceeded context budget. Request a smaller page.]"`
    - _Requirements: 20.1, 20.2, 20.3_

  - [x] 10.5 Implement semantic cache for MCP tool results
    - Cache read operations keyed by SHA-256 of `toolName + JSON-serialised arguments`; TTL: 3,600 s reference data, 30 s entity state, 300 s aggregation
    - Cache hit: increment `mcp.cache.hits` counter; do NOT increment token counters
    - _Requirements: 20.4, 20.6_

  - [x] 10.6 Implement OpenTelemetry token and cost instrumentation
    - On tool call complete: increment `mcp.tokens.input` and `mcp.tokens.output` (tagged `tool.name`, `model.tier`)
    - Set span tags `mcp.tokens.input`, `mcp.tokens.output`, `mcp.cost.usd`
    - _Requirements: 20.5_

  - [x] 10.7 Implement fixed-window rate limiter on MCP gateway
    - Per-authenticated-user: 50 calls/hour; queue up to 5; reject queued calls not dispatched within 30 seconds with HTTP 429 + `Retry-After` header
    - _Requirements: 20.7, 20.8_

- [x] 11. Implement architecture enforcement tests
  - [x] 11.1 Write NetArchTest architecture tests
    - Assert `Orders.Domain` has no dependency on `Orders.Infrastructure` or `Orders.Api`
    - Assert `Orders.Application` has no dependency on `Orders.Infrastructure` or `Orders.Api`
    - Assert `Orders.Infrastructure` has no dependency on `Orders.Api`
    - _Requirements: 9.4_

  - [ ]* 11.2 Write property test for Clean Architecture dependency direction (Property 1)
    - **Property 1: Clean Architecture dependency direction is always enforced** — for any type in Domain, no reference to Application/Infrastructure/Api; for any type in Application, no reference to Infrastructure/Api
    - **Validates: Requirements 1.2, 1.3, 1.4, 9.4**

- [x] 12. Checkpoint — all .NET layers compile, all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 13. Implement LSP shared test base class
  - [x] 13.1 Create shared abstract xUnit test class `OrderRepositoryContractTests<TImpl>`
    - Abstract test method `GetByIdAsync_WhenNotFound_ReturnsNull` that each `IOrderRepository` implementation must extend and pass
    - _Requirements: 2.3_

- [ ] 14. Implement Docker containerization
  - [x] 14.1 Create multi-stage Dockerfile for .NET Orders service
    - Four named stages: `restore`, `build`, `publish`, `final`
    - `final` stage: `adduser` non-root, `USER appuser`, `EXPOSE 8080`, `ENV ASPNETCORE_URLS=http://+:8080`
    - _Requirements: 8.1, 8.2, 18.5_

  - [x] 14.2 Create multi-stage Dockerfile for React SPA
    - Node 20 build stage + Nginx 1.25 final stage; exposes port 80
    - _Requirements: 8.4_

  - [x] 14.3 Create `docker-compose.yml` for local development
    - Services: Orders API, PostgreSQL 16 (healthcheck: interval 5s, timeout 5s, retries 5), RabbitMQ 3.13 (healthcheck: interval 10s, timeout 5s, retries 5), React SPA (host port 3000)
    - Orders API `depends_on` both DB and broker with `condition: service_healthy`
    - _Requirements: 8.3, 8.5, 8.6_

- [x] 15. Implement React frontend architecture
  - [x] 15.1 Scaffold React SPA project with Vite, TypeScript, and folder structure
    - Create `src/features/orders/`, `src/shared/`, `src/lib/`, `src/app/` directories
    - Configure Vite build, TypeScript strict mode
    - _Requirements: 12.1_

  - [x] 15.2 Implement axios HTTP instance with interceptors (`lib/http.ts`)
    - Request interceptor: attaches `Authorization: Bearer <token>` from Zustand auth store if token present
    - Response interceptor: on HTTP 401, redirect to `/login` via `window.location.href`
    - _Requirements: 12.3, 12.4_

  - [ ]* 15.3 Write property test for axios request interceptor (Property 22)
    - **Property 22: Axios request interceptor always attaches Bearer token when token is present** — for any outbound request, if token is in store then `Authorization: Bearer <token>` header is always attached
    - **Validates: Requirements 12.3**

  - [x] 15.4 Implement Zustand auth store
    - Holds `token: string | null`; exposes `setToken` and `clearToken`
    - _Requirements: 12.3_

  - [x] 15.5 Implement `useOrder` and `usePlaceOrder` TanStack Query hooks (`features/orders/api/`)
    - `useOrder(id)`: `useQuery` calling `GET /api/orders/{id}`
    - `usePlaceOrder()`: `useMutation` calling `POST /api/orders`
    - _Requirements: 12.2_

  - [x] 15.6 Implement `PlaceOrderForm` component
    - Submit button `disabled` when `isPending === true`
    - Renders `role="alert"` element with error message when mutation is in error state; removes alert on reset or re-submit
    - _Requirements: 12.5, 12.6_

  - [ ]* 15.7 Write property tests for frontend components (Property 23, Property 24)
    - **Property 23: PlaceOrderForm submit button is always disabled while mutation is pending** — for any `isPending === true` state, button always has `disabled` attribute
    - **Property 24: PlaceOrderForm always renders role="alert" on error and removes it on reset** — error state → alert present; reset/re-submit → alert removed from DOM
    - **Validates: Requirements 12.5, 12.6**

  - [ ]* 15.8 Write Vitest test suite for frontend
    - At least one test each for: `useOrder`, `usePlaceOrder`, pending-disabled button, error-alert behavior
    - Execute via `npm run test -- --run`
    - _Requirements: 12.7_

- [x] 16. Create GitHub repository governance templates and documentation
  - [x] 16.1 Create `.github/CODEOWNERS` template
    - Global fallback: `@enterprise-org/senior-engineers`
    - `Orders.Domain/` and `Orders.Application/`: `@enterprise-org/domain-team`
    - `Orders.Infrastructure/`, `.github/`, `platform-infra/`: `@enterprise-org/platform-team`
    - `frontend/`: `@enterprise-org/frontend-team`
    - _Requirements: 13.1_

  - [x] 16.2 Create `.github/pull_request_template.md`
    - Summary section, Type of Change checklist (bug fix, new feature, refactoring, documentation, dependency update), Checklist (tests, architecture tests, no secrets, CHANGELOG, PR diff ≤ 400 lines)
    - _Requirements: 13.2_

  - [x] 16.3 Create `docs/BRANCH_PROTECTION.md` documenting branch protection rules
    - 1 required approving review, stale-review dismissal, required status checks (build, unit tests, architecture tests, lint), up-to-date requirement, signed commits, push restriction to GitHub Actions service account and admins
    - _Requirements: 13.3_

  - [x] 16.4 Create `docs/REPO_CONVENTIONS.md` documenting polyrepo naming convention and Conventional Commits format
    - Naming: `<service>-service`, `frontend`, `platform-infra` with required top-level folders
    - Conventional Commits: `<type>(<scope>): <description>` with examples and `BREAKING CHANGE:` footer example
    - _Requirements: 13.4, 13.5_

- [x] 17. Create GitHub Actions CI/CD pipeline workflows
  - [x] 17.1 Create four-job `.github/workflows/ci.yml` pipeline
    - Jobs: `lint-and-test` → `build-and-push` → `deploy-staging` → `deploy-production` (each with `needs` on preceding job)
    - `lint-and-test`: restore, build Release, `dotnet test` with coverage, `dorny/test-reporter`, Codecov upload, SonarCloud; fail if coverage < 80%
    - `build-and-push`: OIDC auth via `aws-actions/configure-aws-credentials` with `role-to-assume`; multi-arch build (`linux/amd64`, `linux/arm64`); Trivy scan; fail on CRITICAL vulnerabilities; upload SARIF
    - `deploy-staging`: ECS Fargate deploy; smoke test `GET /health` up to 3 retries / 10s delay; fail if not HTTP 200 within 30s
    - `deploy-production`: manual approval via GitHub Environment `production`; ECS deploy; tag release commit `release-<sha>`
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5, 15.3_

  - [x] 17.2 Create `reusable-dotnet-ci.yml` reusable workflow
    - Callable via `workflow_call`; inputs: `service-path` (required string), `dotnet-version` (optional string, default `8.0.x`); secrets: `SONAR_TOKEN` (required)
    - _Requirements: 14.6_

  - [x] 17.3 Create frontend GitHub Actions workflow (`frontend-ci.yml`)
    - Triggers on push to `main` when paths include `frontend/`
    - Jobs: lint, type-check, Vitest coverage, Vite build, S3 sync, CloudFront invalidation `/*`
    - _Requirements: 14.7_

- [x] 18. Create Architecture Decision Records
  - [x] 18.1 Create `docs/adr/` directory and write five required ADRs
    - `ADR-001-clean-architecture.md`: Title, Status, Context, Decision, Consequences
    - `ADR-002-mediatr-cqrs.md`
    - `ADR-003-masstransit-messaging.md`
    - `ADR-004-outbox-pattern.md`
    - `ADR-005-efcore-orm.md`
    - Each ADR stored as `ADR-NNN-kebab-case-title.md` with zero-padded sequential number
    - _Requirements: 17.1, 17.3_

- [x] 19. Create security documentation and `SECURITY.md` checklist
  - [ ] 19.1 Create `docs/SECURITY.md` with OWASP Top 10 security checklist
    - One entry per A01–A10 category with pass/fail status, notes field, and sign-off section (named reviewer + date)
    - Document: supported secret stores (AWS Secrets Manager, Azure Key Vault), environment-variable injection requirement, prohibition on committing secrets
    - Document: Docker non-root user requirement; weekly base-image rebuild policy
    - _Requirements: 18.3, 18.5, 18.6_

- [x] 20. Create cloud infrastructure topology documentation and sizing docs
  - [x] 20.1 Create `docs/cloud-topology/aws-topology.md`
    - ASCII/Mermaid diagram: Route 53 → CloudFront → WAF → API Gateway → ECS Fargate (BFF, Identity, Orders) → RDS Aurora / DynamoDB / SNS-SQS / ElastiCache Redis; ECR, Secrets Manager, CloudWatch, X-Ray labeled as supporting services
    - _Requirements: 15.1_

  - [x] 20.2 Create `docs/cloud-topology/azure-topology.md`
    - Equivalent Azure topology: Azure Front Door → Static Web Apps → APIM → Container Apps (BFF, Identity, Orders) → Azure SQL / Service Bus / Azure Cache for Redis; ACR, Key Vault, Application Insights labeled
    - Document OIDC federation requirement, IAM trust policy scoping, prohibition on `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` as secrets
    - Document GitHub Environments configuration: `staging` auto-deploy, `production` manual approval, required secrets per environment
    - _Requirements: 15.2, 15.3, 15.4_

  - [x] 20.3 Create `docs/sizing/capacity-estimation.md`
    - Document six-step capacity estimation methodology with formulas
    - Reference numbers table (latency and throughput) covering all required components
    - Fully worked Orders PoC example at 100K DAU with all six steps, formulas, inputs, results, and summary table
    - Architectural decision gates table (8 threshold/trigger rows minimum)
    - Three-scenario scaling projection pattern (Scenario A/B/C) with migration-path requirement
    - _Requirements: 19.1, 19.2, 19.3, 19.4, 19.5_

  - [x] 20.4 Create `docs/llm-cost/llm-cost-estimation.md`
    - Monthly LLM cost estimation methodology using §1.7 back-of-the-envelope applied to token volumes
    - Cost scaling table at 10K / 100K / 1M DAU (with and without caching)
    - Statement on cost-per-DAU threshold ($0.01/day) and required mitigation options
    - _Requirements: 20.9_

- [x] 21. Create bounded context documentation
  - [x] 21.1 Write bounded context specification for Identity, Orders, and Notifications services
    - Each entry: aggregate root(s), published domain events, subscribed domain events
    - Self-contained (no cross-references to external docs)
    - _Requirements: 3.7_

- [x] 22. Validate solution template generates cleanly
  - [x] 22.1 Package the solution as a `dotnet new` template (`template.json`) and verify `dotnet new` + `dotnet build` exits with zero errors, zero elevated warnings, no unresolved placeholder tokens
    - _Requirements: 1.6_

- [x] 23. Final checkpoint — full suite passes
  - Run `dotnet test` and verify all tests pass with zero failures
  - Run `dotnet test --collect:"XPlat Code Coverage"` and verify Domain project line coverage ≥ 80%
  - Run `npm run test -- --run` in the React project and verify zero failures
  - Ensure all docs exist in expected paths
  - Ensure all tests pass, ask the user if questions arise.


---

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP delivery; all correctness properties are still recommended for a reference repository
- Property-based tests use FsCheck; frontend property-like tests use Vitest with parameterised inputs
- Architecture tests (task 11) enforce dependency rules at the assembly level using NetArchTest.Rules
- All C# code targets .NET 8 / C# 12 with nullable reference types enabled
- EF Core uses code-first migrations; no raw SQL string concatenation is permitted anywhere
- Docker images run as non-root; base images must be rebuilt weekly for OS security patches
- GitHub Actions AWS authentication exclusively uses OIDC (no static AWS keys as secrets)
- The Orders/Money domain is the illustrative PoC — not a production service
- Bounded context docs (task 21) are self-contained markdown files, not external links

---

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["2.1", "2.4"] },
    { "id": 1, "tasks": ["2.2", "2.5"] },
    { "id": 2, "tasks": ["2.3", "2.6", "2.8", "2.9"] },
    { "id": 3, "tasks": ["2.7", "3.1", "3.2", "4.1", "4.4"] },
    { "id": 4, "tasks": ["4.2", "4.3", "5.1", "5.3", "5.4", "13.1"] },
    { "id": 5, "tasks": ["5.2", "5.5", "5.6", "5.7", "5.8"] },
    { "id": 6, "tasks": ["7.1", "7.2", "7.3", "8.8", "11.1", "11.2"] },
    { "id": 7, "tasks": ["7.4", "7.5", "8.1", "8.2", "8.3", "8.4", "8.9"] },
    { "id": 8, "tasks": ["7.6", "8.5", "8.6", "8.7", "9.1"] },
    { "id": 9, "tasks": ["9.2", "9.3", "9.5"] },
    { "id": 10, "tasks": ["9.4", "10.1"] },
    { "id": 11, "tasks": ["10.2", "10.3", "10.4", "10.5", "10.6", "10.7"] },
    { "id": 12, "tasks": ["14.1", "14.2", "14.3", "15.1"] },
    { "id": 13, "tasks": ["15.2", "15.4"] },
    { "id": 14, "tasks": ["15.3", "15.5"] },
    { "id": 15, "tasks": ["15.6"] },
    { "id": 16, "tasks": ["15.7", "15.8"] },
    { "id": 17, "tasks": ["16.1", "16.2", "16.3", "16.4", "17.1", "17.2", "17.3", "18.1", "19.1", "20.1", "20.2", "20.3", "20.4", "21.1", "22.1"] }
  ]
}
```
