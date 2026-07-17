# Requirements Document

## Introduction

This document defines the formal requirements for the **Enterprise Application Architecture (EAA) reference repository**. The repository's purpose is normative: it establishes canonical standards, delivers proof-of-concept (PoC) implementations for every major pattern, and provides project templates and reusable CI/CD workflows that downstream development teams use as the starting point for new services and applications.

The requirements are derived from the approved design document and are organized around the repository's three core deliverables:
1. **Standards** — documented architectural decisions and conventions that all downstream projects must follow
2. **PoC implementations** — working, test-covered reference code that demonstrates each pattern
3. **Templates** — scaffolding artefacts (project skeletons, GitHub Actions workflow files, Docker configurations) that downstream teams copy and adapt

---

## Glossary

- **Reference_Repository**: The enterprise-app-architecture repository itself — the system under specification
- **Clean_Architecture_Model**: The four-layer ring model (Domain → Application → Infrastructure → Presentation) defined in §1.2 of the design
- **Domain_Layer**: The innermost Clean Architecture layer containing aggregates, entities, value objects, domain events, repository interfaces, and domain services — with zero external NuGet dependencies
- **Application_Layer**: The second Clean Architecture layer containing CQRS command/query handlers, DTOs, pipeline behaviours, and application event publisher abstractions — referencing only the Domain layer
- **Infrastructure_Layer**: The third Clean Architecture layer containing EF Core, messaging, external HTTP clients, and repository implementations — referencing Domain and Application
- **Presentation_Layer**: The outermost Clean Architecture layer containing Minimal API endpoints, MCP tool handlers, middleware, and Program.cs bootstrap — referencing Application (and Infrastructure for DI wiring only)
- **Order_Aggregate**: The canonical DDD aggregate root used throughout the PoC implementations (Orders service)
- **Money_Value_Object**: The canonical value object used in PoC code to represent a currency amount
- **Outbox_Processor**: The background service that polls the outbox table and publishes domain events reliably
- **MCP_Gateway**: The .NET Minimal API service that exposes AI-callable tools over the Model Context Protocol
- **CI_Pipeline**: The four-job GitHub Actions workflow (lint-and-test → build-and-push → deploy-staging → deploy-production)
- **Reusable_Workflow**: A GitHub Actions `workflow_call` YAML file that encapsulates shared CI steps for consumption by per-service workflows
- **PoC**: Proof-of-concept — a working, compilable, and test-covered implementation that demonstrates a pattern
- **ADR**: Architecture Decision Record — a short document capturing a significant architectural decision, its context, and its rationale
- **Downstream_Team**: Any product development team that adopts the reference repository's patterns, templates, or workflows
- **Architecture_Test**: An automated test using NetArchTest.Rules that enforces dependency-direction rules at the assembly level
- **Property_Test**: An automated test using FsCheck that validates a universal invariant across a wide range of generated inputs
- **Specification_Pattern**: An encapsulated, composable business-rule predicate expressed as an `Expression<Func<T, bool>>`
- **Outbox_Pattern**: The reliability pattern in which domain events are persisted in the same database transaction as the aggregate change and later published by the Outbox_Processor
- **Capacity_Estimation**: The six-step back-of-the-envelope methodology (load profile → QPS → storage → bandwidth → latency budget → decision gates) applied before committing to an architecture decision
- **Decision_Gate**: A threshold derived from capacity estimation that triggers a specific architectural choice (e.g., adding a caching layer when peak read QPS ≥ 1,000)
- **Model_Tier**: A classification of LLM models by capability and cost (Lightweight, Standard, Heavy) used to route MCP tool calls to the most cost-effective model
- **Token_Budget**: The maximum number of input tokens allocated to a single MCP tool call, divided across system prompt, tool schemas, conversation history, and result payload
- **Semantic_Cache**: A distributed cache keyed by a hash of the tool name and arguments that stores deterministic LLM tool results to avoid redundant API calls

---

## Requirements

### Requirement 1: Clean Architecture Layer Structure

**User Story:** As a downstream team lead, I want a canonical four-layer Clean Architecture project structure, so that every new service starts from a consistent, dependency-rule-compliant skeleton.

#### Acceptance Criteria

1. THE Reference_Repository SHALL provide a .NET solution template containing four projects: `<Service>.Domain`, `<Service>.Application`, `<Service>.Infrastructure`, and `<Service>.Api`.
2. THE Domain_Layer SHALL have zero compile-time references to Application_Layer, Infrastructure_Layer, or Presentation_Layer assemblies.
3. THE Application_Layer SHALL have no compile-time reference to Infrastructure_Layer or Presentation_Layer assemblies.
4. THE Infrastructure_Layer SHALL reference Domain_Layer and Application_Layer but SHALL NOT reference Presentation_Layer.
5. THE Presentation_Layer SHALL reference Application_Layer. IF the Presentation_Layer references Infrastructure_Layer, THEN those references SHALL be confined exclusively to `Program.cs` and SHALL NOT appear in any other file in the Presentation_Layer project.
6. WHEN a downstream team invokes `dotnet new` with the solution template, THE resulting solution SHALL compile with `dotnet build` exiting with zero build errors, zero warnings elevated to errors, and no unresolved placeholder tokens in any project file.

---

### Requirement 2: SOLID Principles Reference Implementations

**User Story:** As a developer onboarding to the platform, I want working C# code examples for each SOLID principle, so that I can learn and apply the correct patterns in my service.

#### Acceptance Criteria

1. THE Reference_Repository SHALL include a PoC demonstrating the Single Responsibility Principle by dispatching an in-process `OrderPlacedEvent` from `PlaceOrderHandler` via `IApplicationEventPublisher`, with a separate `OrderPlacedNotificationHandler` consuming that event to send the email notification, so that both classes can be tested independently with no shared mutable state.
2. THE Reference_Repository SHALL include a PoC demonstrating the Open/Closed Principle by providing an `IDiscountStrategy` interface and at least two concrete strategy implementations (`SeasonalDiscountStrategy`, `LoyaltyDiscountStrategy`) injected into `PricingService` via constructor injection, so that a new strategy can be registered without modifying `PricingService`.
3. THE Reference_Repository SHALL include a PoC demonstrating the Liskov Substitution Principle by providing a shared abstract xUnit test class with a `GetByIdAsync_WhenNotFound_ReturnsNull` test case that each `IOrderRepository` implementation must extend and pass, enforcing the null-return contract on all implementations.
4. THE Reference_Repository SHALL include a PoC demonstrating the Interface Segregation Principle by splitting a broad order-service interface into `IOrderWriter` (containing at minimum `PlaceOrder` and `CancelOrder`), `IOrderReader` (containing at minimum `GetOrder`), and `IOrderExporter` (containing at minimum `ExportToPdf`) interfaces with no method appearing in more than one interface.
5. THE Reference_Repository SHALL include a PoC demonstrating the Dependency Inversion Principle by registering all abstractions (`IOrderRepository`, `IApplicationEventPublisher`) against concrete implementations in `Program.cs` via the built-in .NET DI container.
6. WHEN the SOLID PoC tests are executed, THE Reference_Repository SHALL produce a passing test suite with at least one test per principle.

---

### Requirement 3: Domain-Driven Design Building Blocks

**User Story:** As a domain engineer, I want reference implementations of DDD building blocks, so that I can model new bounded contexts consistently.

#### Acceptance Criteria

1. THE Reference_Repository SHALL provide an `Order` aggregate root whose `Create` factory method throws an `OrderDomainException` when the supplied `lines` collection is empty, preventing any `Order` instance from existing with zero `OrderLine` entries.
2. THE Reference_Repository SHALL provide a `Money` value object implemented as an immutable C# `record` that throws an `ArgumentException` when constructed with a negative amount or with a currency code that is not exactly three uppercase letters conforming to ISO 4217.
3. THE Reference_Repository SHALL provide a `DomainEvent` base record and at least three concrete event types (`OrderCreatedEvent`, `OrderPlacedEvent`, `OrderCancelledEvent`) raised by the `Order` aggregate.
4. THE Reference_Repository SHALL provide an `IOrderRepository` interface in the Domain_Layer with `GetByIdAsync`, `GetByCustomerAsync`, `SaveAsync`, and `DeleteAsync` operations.
5. WHEN `Order.Place()` is called on an `Order` whose `Status` is not `OrderStatus.Pending`, THE Order_Aggregate SHALL throw an `OrderDomainException` with a message identifying the invalid transition.
6. WHEN `Order.Cancel()` is called on an `Order` whose `Status` is `OrderStatus.Shipped` or `OrderStatus.Cancelled`, THE Order_Aggregate SHALL throw an `OrderDomainException` with a message identifying the invalid transition.
7. THE Reference_Repository SHALL document bounded context boundaries with at least the Identity, Orders, and Notifications service examples, each specifying its aggregate root(s), published domain events, and subscribed domain events without relying on external cross-references.
8. THE valid `Order` status lifecycle SHALL be: `Pending → Placed`, `Placed → Shipped`, `Pending → Cancelled`, `Placed → Cancelled`; all other transitions SHALL be rejected by throwing an `OrderDomainException`.

---

### Requirement 4: Application Layer — CQRS with MediatR

**User Story:** As a developer building a feature, I want CQRS command and query handler patterns with pipeline behaviours, so that I can implement use cases with consistent cross-cutting concerns.

#### Acceptance Criteria

1. THE Reference_Repository SHALL provide `PlaceOrderCommand` and `PlaceOrderHandler` as a PoC command/handler pair conforming to the MediatR `IRequest<TResponse>` / `IRequestHandler<TRequest, TResponse>` interfaces.
2. THE Reference_Repository SHALL provide `GetOrderQuery` (accepting an order identifier parameter) and `GetOrderHandler` as a PoC query/handler pair.
3. IF a registered FluentValidation validator for the request type produces one or more `ValidationFailure` entries, THEN THE `ValidationBehaviour<TRequest, TResponse>` SHALL throw a `FluentValidation.ValidationException` containing all failures before the next pipeline step executes.
4. THE Reference_Repository SHALL provide a `LoggingBehaviour<TRequest, TResponse>` pipeline behaviour that logs at `Information` level the request type name before delegation and after the response is returned.
5. WHEN a `PlaceOrderCommand` with an empty `Lines` collection is dispatched through the MediatR pipeline, THE `ValidationBehaviour` SHALL throw a `FluentValidation.ValidationException` such that a test asserting `_repo.SaveAsync` was never called (via Moq `Times.Never`) passes.
6. THE Reference_Repository SHALL demonstrate registration of pipeline behaviours in `Program.cs` using `MediatR.AddBehavior`.
7. THE pipeline behaviour registration order SHALL place `LoggingBehaviour` as the outermost behaviour and `ValidationBehaviour` as the innermost behaviour before the handler, so that logging wraps validation in all execution traces.

---

### Requirement 5: RESTful Minimal API Endpoints

**User Story:** As an API consumer, I want well-defined RESTful endpoints with OpenAPI documentation, so that I can integrate reliably with the Orders service.

#### Acceptance Criteria

1. THE Reference_Repository SHALL provide a `POST /api/orders` endpoint that accepts a JSON body conforming to the `PlaceOrderRequest` schema and returns HTTP 201 with a `Location` header set to `/api/orders/{id}` and a JSON body containing the new order's UUID on success.
2. THE Reference_Repository SHALL provide a `GET /api/orders/{id}` endpoint that returns HTTP 200 with an `OrderDto` when the order exists and HTTP 404 when it does not.
3. THE Reference_Repository SHALL provide a `DELETE /api/orders/{id}` endpoint that cancels an order and returns HTTP 204 on success and HTTP 409 when the order's `Status` is any value other than `Pending`.
4. THE Reference_Repository SHALL map all endpoint routes using the `MapGroup` + extension-method pattern (`MapOrdersEndpoints`) rather than controllers.
5. THE Reference_Repository SHALL register `WithOpenApi()`, `WithSummary(string)`, and at least one `Produces<T>(int)` annotation on every endpoint so that the generated OpenAPI document includes a summary and at least one declared response schema per operation.
6. WHEN a request body fails FluentValidation, THE Presentation_Layer SHALL return HTTP 400 with a `ProblemDetails` body whose `errors` dictionary contains at least one entry keyed by the failing field name.
7. WHEN `ExceptionHandlingMiddleware` catches a `DomainException` thrown during handler execution, THE Presentation_Layer SHALL return HTTP 422 with a `ProblemDetails` body whose `detail` field contains the exception message.
8. WHEN an unhandled exception reaches `ExceptionHandlingMiddleware`, THE Presentation_Layer SHALL return HTTP 500 with a `ProblemDetails` body and log the exception at `Critical` level including the full stack trace.

---

### Requirement 6: Design Pattern Reference Implementations

**User Story:** As a developer, I want reference implementations of common enterprise design patterns, so that I can apply them correctly in my service.

#### Acceptance Criteria

1. THE Reference_Repository SHALL provide an `EfOrderRepository` implementing the Repository Pattern using EF Core 8 that eager-loads `OrderLine` entities via `.Include(o => o.Lines)` on every query that returns an `Order`, so that `order.Lines` is never null or empty due to lazy-loading omission.
2. THE Reference_Repository SHALL demonstrate the Factory Pattern via `Order.Create(...)` and `OrderLine.Create(...)` static factory methods, each of which throws an `OrderDomainException` when its domain invariants are violated, so that no invalid `Order` or `OrderLine` instance can be constructed via `new`.
3. THE Reference_Repository SHALL provide a complete Outbox Pattern implementation: `OutboxMessage` mapped to the `outbox_messages` table, domain events serialised into `OutboxMessage` rows within the same `SaveChangesAsync` call as the aggregate, and an `OutboxProcessor` background service that polls every 5 seconds, publishes pending messages via MassTransit, and marks each message `ProcessedAt = UtcNow` in the same database transaction as the publish acknowledgement.
4. THE Reference_Repository SHALL provide a `CachedOrderRepository` demonstrating the Decorator Pattern that wraps `IOrderRepository` with `IDistributedCache`: on cache miss it calls the inner repository and stores the result with a five-minute absolute expiry; on cache hit it returns the cached value without calling the inner repository.
5. THE Reference_Repository SHALL demonstrate the Strategy Pattern through `IDiscountStrategy`, `SeasonalDiscountStrategy`, `LoyaltyDiscountStrategy`, and `PricingService`, where `PricingService.Calculate` applies each strategy sequentially via `Aggregate` and the result is always ≤ the input price.
6. THE Reference_Repository SHALL provide a `Specification<T>` abstract base class with a `ToExpression()` method returning `Expression<Func<T, bool>>` and a `PendingOrdersSpecification` whose expression matches only `Order` records where `Status == OrderStatus.Pending`.
7. WHEN the `OutboxProcessor` catches any exception during publish or transaction commit for a specific message, THE `OutboxProcessor` SHALL roll back that message's transaction, log the exception at `Error` level including the `OutboxMessage.Id`, and leave `ProcessedAt` null so the message is retried in the next polling cycle.

---

### Requirement 7: MCP Gateway Integration

**User Story:** As an AI/LLM agent developer, I want a Model Context Protocol gateway that exposes domain operations as structured tools, so that AI agents can invoke business logic safely through a well-defined interface.

#### Acceptance Criteria

1. THE Reference_Repository SHALL provide a .NET Minimal API MCP_Gateway that registers MCP tools using `AddMcpServer().WithHttpTransport().WithTools<T>()`.
2. WHEN the `get_order` MCP tool is called with a valid UUID that corresponds to an existing order, THE MCP_Gateway SHALL return a `CallToolResult` whose `content[0].text` is the JSON serialization of the matching `OrderDto`.
3. WHEN the `get_order` MCP tool is called with a UUID that does not correspond to any order, THE MCP_Gateway SHALL return a `CallToolResult` whose `content[0].text` is a human-readable sentence of the form `"No order found with ID {orderId}."`.
4. WHEN the `place_order` MCP tool is called with a valid `customerId` UUID and a well-formed `linesJson` array, THE MCP_Gateway SHALL return a `CallToolResult` whose `content[0].text` is a sentence of the form `"Order placed successfully. Order ID: {id}"`.
5. WHEN the `place_order` MCP tool is called with a `linesJson` value that is not valid JSON or does not conform to the order-line schema, THE MCP_Gateway SHALL return a `CallToolResult` whose `content[0].text` describes the validation error without exposing an unhandled exception stack trace.
6. THE Reference_Repository SHALL include a JSON tool-manifest document defining the input schemas for all registered MCP tools, with each tool entry containing `name`, `description`, and an `inputSchema` object with `type: "object"`, `properties`, and `required` fields.
7. WHEN an MCP tool call causes a `DomainException` to be thrown during handler execution, THE MCP_Gateway SHALL catch it and return a `CallToolResult` whose `content[0].text` is the exception message rather than propagating an HTTP 500 response.

---

### Requirement 8: Docker Containerization

**User Story:** As a platform engineer, I want multi-stage Dockerfiles and a Docker Compose configuration, so that any service can be built into a minimal container image and run locally with all dependencies.

#### Acceptance Criteria

1. THE Reference_Repository SHALL provide a multi-stage Dockerfile for .NET services with four named stages: `restore`, `build`, `publish`, and `final`.
2. THE Reference_Repository SHALL configure the `final` Docker stage to run as a non-root user and expose port 8080.
3. THE Reference_Repository SHALL provide a `docker-compose.yml` that starts the Orders API, a PostgreSQL 16 database with `interval: 5s`, `timeout: 5s`, `retries: 5` health-check parameters, and a RabbitMQ 3.13 broker with `interval: 10s`, `timeout: 5s`, `retries: 5` health-check parameters, with the Orders API declared `depends_on` those services with `condition: service_healthy`.
4. THE Reference_Repository SHALL provide a multi-stage Dockerfile for the React SPA using a Node 20 build stage and an Nginx 1.25 final stage that exposes port 80.
5. WHEN `docker compose up` is executed in the repository root, THEN within 120 seconds all services SHALL report a Docker health status of `healthy` with no manual intervention required.
6. THE `docker-compose.yml` SHALL include the React SPA service mapped to host port 3000, so that the full system (API + database + broker + frontend) can be started with a single `docker compose up` command.

---

### Requirement 9: Unit and Property-Based Testing Strategy

**User Story:** As a quality engineer, I want a comprehensive testing strategy with domain unit tests, handler tests, property-based tests, and architecture enforcement tests, so that every layer of the architecture is automatically verified.

#### Acceptance Criteria

1. THE Reference_Repository SHALL provide domain unit tests using xUnit that cover `Order` aggregate construction, `Place`, and `Cancel` transitions — including both happy-path and exception scenarios — without using any test doubles.
2. WHEN `PlaceOrderHandler.Handle` is called with a valid `PlaceOrderCommand` (an `Order` that can be created and placed without throwing), THE handler SHALL call `IOrderRepository.SaveAsync` exactly once and `IApplicationEventPublisher.PublishAsync` exactly once, as verified by Moq `Times.Once()` assertions.
3. THE Reference_Repository SHALL provide property-based tests using FsCheck that verify `Money` addition commutativity (`(a + b).Amount == (b + a).Amount`) and that the result amount is ≥ 0.00 for all generated non-negative input pairs.
4. THE Reference_Repository SHALL provide architecture enforcement tests using NetArchTest.Rules that assert the Domain_Layer has no dependency on Infrastructure or Api assemblies and that the Application_Layer has no dependency on the Infrastructure assembly.
5. THE Reference_Repository SHALL provide a shared `OrderFaker` test-data builder using Bogus that generates `Order` aggregates satisfying: non-null `OrderId`, at least one `OrderLine`, and `Total.Amount ≥ 0.00`, in both `Pending` and `Placed` states.
6. WHEN all test projects are executed via `dotnet test`, THE Reference_Repository SHALL produce a fully passing test suite with zero failures.
7. WHEN `dotnet test` is executed with `--collect:"XPlat Code Coverage"`, THE Reference_Repository SHALL produce a Cobertura XML coverage report, and the line coverage for the `Orders.Domain` project SHALL be ≥ 80%.

---

### Requirement 10: EF Core Infrastructure Configuration

**User Story:** As a developer configuring data persistence, I want EF Core entity type configurations for the Orders aggregate, so that domain value objects and owned entities map correctly to relational tables.

#### Acceptance Criteria

1. WHEN the EF Core model is configured, THE `OrderEntityTypeConfiguration` SHALL map the `Order` aggregate to the `orders` table with strongly-typed ID conversions for `OrderId` and `CustomerId`, so that a round-trip persist-and-retrieve returns an `Order` with `Id` and `CustomerId` values equal to the originals.
2. WHEN the EF Core model is configured, THE `Money` value object SHALL be mapped as an owned entity so that a round-trip persist-and-retrieve returns a `Money` value with `Amount` and `Currency` equal to the originals without requiring a separate join.
3. WHEN the EF Core model is configured, THE `OrderLine` collection SHALL be mapped as an owned-many entity in a separate `order_lines` table with orphan deletion enabled, so that removing an `OrderLine` from the aggregate and calling `SaveChangesAsync` deletes the corresponding row.
4. WHEN the EF Core model is configured, THE `Order.Lines` navigation SHALL use `PropertyAccessMode.Field` targeting the private `_lines` backing field, so that EF Core populates the collection without requiring a public setter.
5. WHEN the EF Core model is configured, THE `OutboxMessage` entity SHALL be mapped to an `outbox_messages` table where `Id`, `EventType`, `Payload`, and `OccurredAt` are non-nullable columns and `ProcessedAt` is a nullable column, so that unprocessed messages have `ProcessedAt IS NULL` and processed messages have a non-null UTC timestamp.

---

### Requirement 11: Microservices Communication Patterns

**User Story:** As a platform architect, I want reference implementations for both synchronous HTTP and asynchronous messaging between services, so that downstream teams have a proven inter-service communication pattern to follow.

#### Acceptance Criteria

1. THE Reference_Repository SHALL provide a typed HTTP client (`InventoryHttpClient`) registered via `AddHttpClient` and configured with `AddStandardResilienceHandler` to demonstrate synchronous service-to-service communication, where the resilience policy includes automatic retry and circuit-breaker behaviour as defined by the standard handler defaults.
2. THE Reference_Repository SHALL provide a `MassTransitEventPublisher` implementing `IApplicationEventPublisher` that delegates to `IPublishEndpoint` to demonstrate asynchronous domain event publishing.
3. THE Reference_Repository SHALL provide an `OrderPlacedConsumer` implementing `IConsumer<OrderPlacedEvent>` in the Notifications service to demonstrate asynchronous message consumption.
4. IF the message bus is unavailable and all retries configured by the standard resilience handler are exhausted, THEN THE Infrastructure_Layer SHALL return an HTTP 503 response containing an error message indicating service unavailability to the caller.
5. IF the `InventoryHttpClient` target is unavailable and all retries configured by the standard resilience handler are exhausted, THEN THE Infrastructure_Layer SHALL return an HTTP 503 response containing an error message indicating service unavailability to the caller.

---

### Requirement 12: React Frontend Architecture

**User Story:** As a frontend developer, I want a React 18 + TypeScript reference implementation with TanStack Query and Zustand, so that I have a proven frontend pattern aligned with the backend architecture.

#### Acceptance Criteria

1. THE Reference_Repository SHALL provide a React SPA project using React 18, TypeScript, and Vite with the feature-module folder structure: `src/features/`, `src/shared/`, `src/lib/`, and `src/app/`.
2. THE Reference_Repository SHALL provide `useOrder` and `usePlaceOrder` hooks in `features/orders/api/` implemented with TanStack Query `useQuery` and `useMutation` respectively.
3. WHEN the axios HTTP instance in `lib/http.ts` sends a request, THE request interceptor SHALL attach a `Authorization: Bearer <token>` header sourced from the Zustand auth store if a token is present.
4. IF the axios response interceptor receives an HTTP 401 response, THEN THE interceptor SHALL redirect the browser to `/login` via `window.location.href`.
5. WHILE the `usePlaceOrder` mutation is in a pending state (`isPending === true`), THE `PlaceOrderForm` submit button SHALL have its `disabled` attribute set to `true`.
6. IF the `usePlaceOrder` mutation transitions to an error state, THEN THE `PlaceOrderForm` SHALL render an element with `role="alert"` containing the error message; WHEN the mutation is reset or re-submitted, THE alert element SHALL be removed from the DOM.
7. WHEN the frontend test suite is executed via `npm run test -- --run`, THE Reference_Repository SHALL produce a passing Vitest test suite with at least one test for each of `useOrder`, `usePlaceOrder`, the pending-disabled button behavior, and the error-alert behavior, with zero failures.

---

### Requirement 13: GitHub Repository and Governance Standards

**User Story:** As a platform lead, I want documented and enforced GitHub repository standards, so that every service repository has consistent branch protection, code ownership, and pull-request hygiene.

#### Acceptance Criteria

1. THE Reference_Repository SHALL provide a `.github/CODEOWNERS` template that assigns `@enterprise-org/senior-engineers` as the global fallback reviewer and specifies `@enterprise-org/domain-team` for `Orders.Domain/` and `Orders.Application/` paths, `@enterprise-org/platform-team` for `Orders.Infrastructure/`, `.github/`, and `platform-infra/` paths, and `@enterprise-org/frontend-team` for `frontend/` paths.
2. THE Reference_Repository SHALL provide a `.github/pull_request_template.md` containing: a Summary section, a Type of Change checklist (bug fix, new feature, refactoring, documentation, dependency update), and a Checklist with items for relevant tests added or updated and passing, architecture tests passing, no secrets or credentials committed (verified by automated secret scanning), breaking changes documented in CHANGELOG.md, and PR diff ≤ 400 lines.
3. THE Reference_Repository SHALL document branch-protection rules for `main` specifying: minimum 1 required approving review, stale-review dismissal on new commits, required passing status checks including build, unit tests, architecture tests, and lint, branches must be up to date before merging, commits must be signed, and direct pushes restricted to the GitHub Actions service account and repository admins.
4. THE Reference_Repository SHALL document the polyrepo repository naming convention requiring service repositories to be named `<service>-service`, the frontend repository named `frontend`, and the infrastructure repository named `platform-infra`, each containing `src/`, `tests/`, `.github/`, and `docs/` as top-level folders.
5. THE Reference_Repository SHALL document the Conventional Commits message format specifying that every commit message MUST use the structure `<type>(<scope>): <description>` with at least these examples: `feat(orders): add cancellation endpoint`, `fix(money): handle zero-amount edge case`, `chore(deps): update EF Core to 8.0.x`, and a breaking-change example using a `BREAKING CHANGE:` footer.

---

### Requirement 14: GitHub Actions CI/CD Pipeline

**User Story:** As a DevOps engineer, I want a complete four-job GitHub Actions pipeline for .NET services and a reusable workflow template, so that every service gets consistent build, test, security scan, and deployment automation.

#### Acceptance Criteria

1. THE Reference_Repository SHALL provide a GitHub Actions workflow with four jobs where each job declares `needs` on the preceding job's success, so that `build-and-push` runs only after `lint-and-test` passes, `deploy-staging` runs only after `build-and-push` passes, and `deploy-production` runs only after `deploy-staging` passes.
2. THE `lint-and-test` job SHALL restore dependencies, build in Release mode, run all unit and integration tests in the repository with code coverage, publish test results using `dorny/test-reporter`, upload coverage to Codecov, and run SonarCloud analysis; the job SHALL fail if line coverage falls below 80%.
3. THE `build-and-push` job SHALL authenticate to AWS ECR using OIDC with `role-to-assume` (never using `AWS_ACCESS_KEY_ID` or `AWS_SECRET_ACCESS_KEY`), build and push a Docker image for both `linux/amd64` and `linux/arm64` architectures, run a Trivy scan, and fail the job if any CRITICAL severity vulnerability is found, uploading the SARIF report to GitHub Security.
4. THE `deploy-staging` job SHALL deploy to ECS Fargate, wait up to 10 minutes for service stability, and execute a smoke test that retries the `GET /health` endpoint up to 3 times with a 10-second delay, failing the job if HTTP 200 is not received within 30 seconds of the first attempt.
5. THE `deploy-production` job SHALL require manual approval via a GitHub Environment named `production` before executing the ECS deployment and tagging the release commit with `release-<sha>` via the GitHub API.
6. THE Reference_Repository SHALL provide a `reusable-dotnet-ci.yml` workflow callable via `workflow_call` that accepts a required `service-path` string input, an optional `dotnet-version` string input in `X.Y` or `X.Y.Z` format defaulting to `8.0.x`, and a required `SONAR_TOKEN` secret.
7. WHEN a commit is merged to `main` and the changed paths include files under `frontend/`, THE frontend GitHub Actions workflow SHALL run lint, type-check, Vitest tests with coverage, build the Vite project, sync the `dist/` output to the S3 bucket, and create a CloudFront invalidation for `/*`.
8. IF the `lint-and-test` job exits with a non-zero status on a pull request, THEN the pull request SHALL not be mergeable into `main`.

---

### Requirement 15: Cloud Infrastructure Topology Documentation

**User Story:** As a cloud architect, I want documented AWS and Azure topology diagrams and configuration patterns, so that I can provision equivalent environments on either cloud provider.

#### Acceptance Criteria

1. THE Reference_Repository SHALL provide an AWS topology diagram with labeled nodes and directional connectivity arrows showing the flow: Route 53 → CloudFront (React SPA, S3 origin) → WAF → API Gateway → ECS Fargate (BFF, Identity, Orders) → RDS Aurora (Multi-AZ) / DynamoDB / SNS/SQS / ElastiCache Redis, with ECR, Secrets Manager, CloudWatch, and X-Ray identified as supporting services.
2. THE Reference_Repository SHALL provide an Azure topology diagram with labeled nodes and directional connectivity arrows showing the equivalent flow: Azure Front Door → Static Web Apps → Azure APIM → Container Apps Environment (BFF, Identity, Orders) → Azure SQL / Service Bus / Azure Cache for Redis, with ACR, Key Vault, and Application Insights identified as supporting services.
3. THE Reference_Repository SHALL document that all GitHub Actions AWS authentication uses OIDC federation via `aws-actions/configure-aws-credentials` with `role-to-assume`, that the IAM trust policy MUST be scoped to specific repository references (not the entire organization), and that `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` MUST NOT be stored as GitHub secrets.
4. THE Reference_Repository SHALL document GitHub Environments configuration specifying: `staging` auto-deploys on merge to `main` with no approval gate; `production` requires manual approval from `@enterprise-org/platform-team`; and the following secrets with their environment scope: `AWS_ECR_REGISTRY` (both), `AWS_DEPLOY_ROLE` (both), `AZURE_CLIENT_ID` (both), `AZURE_TENANT_ID` (both), `AZURE_SUBSCRIPTION_ID` (both), `SONAR_TOKEN` (repository-level).

---

### Requirement 16: Observability Standards

**User Story:** As a site reliability engineer, I want structured logging and distributed tracing wired into the reference service, so that downstream teams inherit production-ready observability from day one.

#### Acceptance Criteria

1. THE Reference_Repository SHALL configure Serilog in `Program.cs` with JSON console output, an OpenTelemetry sink, and a `Service` log property set to the application name sourced from host configuration, so that every log entry contains a `Service` field identifying the emitting service.
2. THE Reference_Repository SHALL configure OpenTelemetry with ASP.NET Core and EF Core tracing instrumentation and ASP.NET Core metrics instrumentation exported via OTLP, so that traces and metrics are observable by any OTLP-compatible backend without additional code changes.
3. WHEN `PlaceOrderHandler.Handle` is invoked, THE handler SHALL create a child span named `"PlaceOrder"` via `ActivitySource.StartActivity` and set a tag with key `"customer.id"` and value equal to the command's `CustomerId`, so that the span is present in any OTLP trace for that operation.
4. THE Reference_Repository SHALL configure the minimum log level in Production to `Information`, and the following log-level-to-event mapping SHALL be enforced: `Information` for request received and request completed, `Warning` for domain exceptions, `Error` for infrastructure exceptions (database, HTTP client), `Critical` for unhandled exceptions, and `Debug` suppressed entirely in Production.

---

### Requirement 17: Architecture Decision Records

**User Story:** As a future maintainer, I want Architecture Decision Records documenting significant technology choices, so that I understand the rationale behind the platform's design and can evaluate alternatives with full context.

#### Acceptance Criteria

1. THE Reference_Repository SHALL contain ADRs for at least the following decisions: adoption of Clean Architecture, choice of MediatR for CQRS, adoption of MassTransit for messaging abstraction, use of the Outbox Pattern for reliable event delivery, and selection of EF Core 8 as the ORM; each ADR SHALL contain at minimum the sections: Title, Status, Context, Decision, and Consequences.
2. WHEN a significant architectural decision is changed (where "significant" means any change to project structure, cross-cutting concerns, technology selection, or integration patterns), THE Reference_Repository SHALL update the affected ADR's Status to `Superseded by ADR-NNN`, create a new ADR for the replacement decision referencing the superseded ADR in its Context section, and preserve the original ADR file unchanged.
3. THE Reference_Repository SHALL store all ADRs in `docs/adr/` using the filename format `ADR-NNN-kebab-case-title.md` where `NNN` is a zero-padded sequential number starting at `001` (e.g., `ADR-001-clean-architecture.md`, `ADR-002-mediatr-cqrs.md`).

---

### Requirement 18: Security Standards

**User Story:** As a security engineer, I want the reference repository to embed security best practices into every layer, so that downstream services inherit a secure-by-default posture.

#### Acceptance Criteria

1. THE Presentation_Layer SHALL apply `RequireAuthorization()` to all endpoint groups and SHALL only omit authorization for endpoints explicitly annotated with `AllowAnonymous()`.
2. THE Reference_Repository SHALL configure JWT Bearer authentication in `Program.cs` sourcing `Authority` and `Audience` from the .NET configuration system (e.g., `IConfiguration`), so that no string literals representing authority URLs or audience values appear anywhere in source code.
3. THE Reference_Repository SHALL include documentation stating: (a) which secrets stores are supported (AWS Secrets Manager and Azure Key Vault), (b) that secrets MUST be injected via environment variables or the .NET configuration provider at runtime, and (c) that committing secrets or credentials to source control is prohibited.
4. THE Infrastructure_Layer SHALL use EF Core parameterised queries exclusively; raw SQL string concatenation with user-supplied input SHALL NOT appear anywhere in the reference code.
5. THE Reference_Repository SHALL configure Docker images to run as a non-root user, and the repository documentation SHALL state that each Docker base image MUST be rebuilt at least once per calendar week to incorporate OS security patches.
6. THE Reference_Repository SHALL include a `docs/SECURITY.md` file containing a security checklist with one entry per OWASP Top 10 category (A01 through A10), each entry having pass/fail status and a notes field, plus a sign-off section requiring a named reviewer and date; this sign-off MUST be present and completed before the first production deployment of any downstream service.

---

### Requirement 19: System Sizing and Capacity Estimation

**User Story:** As a downstream service architect, I want a documented capacity estimation methodology with reference numbers and worked examples, so that every new service starts from a consistent, evidence-based sizing decision rather than guesswork.

#### Acceptance Criteria

1. THE Reference_Repository SHALL document a six-step capacity estimation methodology covering: (1) load profile definition (DAU, peak-to-average ratio, read/write ratio, request size), (2) QPS derivation (average and peak), (3) storage estimation (record size, daily and annual growth, replication factor), (4) bandwidth estimation (inbound and outbound at peak), (5) latency budget calculation (end-to-end SLA minus fixed overheads), and (6) architectural decision mapping that produces a list of triggered gates with their corresponding architectural responses.
2. THE Reference_Repository SHALL provide a reference numbers table where each entry includes the component name, a representative order-of-magnitude value, and its unit (µs or ms for latency; RPS or MB/s for throughput), covering at minimum: L1 cache read, RAM read, SSD random read, Redis read, intra-region network round-trip, PostgreSQL indexed read, external HTTP call, single Fargate task throughput, PostgreSQL read throughput, PostgreSQL write throughput, Redis throughput, and SNS/SQS message throughput.
3. THE Reference_Repository SHALL provide a fully worked capacity estimation example for the Orders PoC at 100,000 DAU where each of the six methodology steps shows: the formula applied, the input values substituted, and the numeric result, culminating in a summary table containing peak QPS, annual storage, peak bandwidth, latency headroom, compute requirement, and DB requirement.
4. THE Reference_Repository SHALL provide an architectural decision gates table where every row specifies a metric name, threshold value, and architectural trigger, covering at minimum: read QPS ≥ 1,000 → add caching layer; read QPS ≥ 10,000 → add separate read store; write QPS ≥ 500 → consider async write path; write QPS ≥ 5,000 → consider DB sharding; annual storage ≥ 500 GB → plan partitioning or archiving; p99 latency budget remaining ≤ 50 ms → caching mandatory; p99 latency budget remaining ≤ 10 ms → in-process cache required; payload size p99 ≥ 100 KB → add pagination or CDN.
5. THE Reference_Repository SHALL document a three-scenario scaling projection pattern (Scenario A: launch DAU; Scenario B: 12-month DAU; Scenario C: worst-case = 10× peak launch DAU) that downstream teams must apply before go-live, specifying that if Scenario C crosses any decision gate threshold the architecture must either handle that scale from day one or provide a migration path document stating the trigger metric value, the target architecture, and the estimated effort in person-days.

---

### Requirement 20: LLM Cost and Context Management

**User Story:** As a platform engineer operating the MCP Gateway, I want model tier selection, context budgeting, response caching, token observability, and rate limiting implemented in the reference service, so that LLM API costs are predictable, attributable, and bounded in all downstream services that adopt the MCP pattern.

#### Acceptance Criteria

1. THE MCP_Gateway SHALL select the LLM model for each tool call based on a configurable tier map (Lightweight, Standard, Heavy), defaulting to the Lightweight tier when no mapping exists, so that simple lookup tools never invoke a Tier 2 or Tier 3 model unnecessarily.
2. THE MCP_Gateway SHALL enforce a per-tool-call context budget of 8,000 tokens (configurable), allocated as: 500 tokens for the system prompt, 500 tokens for tool schemas, 2,000 tokens for conversation history (sliding window — oldest turns dropped first when limit is exceeded), 4,000 tokens for tool result payload, and 1,000 tokens safety margin.
3. WHEN a tool result payload exceeds the 4,000-token result budget, THE MCP_Gateway SHALL truncate the serialised result at the budget boundary and append the literal suffix `"\n[...truncated: result exceeded context budget. Request a smaller page.]"`, rather than exceeding the budget or discarding the result entirely.
4. THE MCP_Gateway SHALL cache the result of a tool call when the tool is not a write operation and not a reasoning/generation tool, using a distributed cache entry keyed by the SHA-256 hash of the concatenation of the tool name and the JSON-serialised arguments, with a TTL of: 3,600 seconds for reference data tools, 30 seconds for entity state tools, and 300 seconds for aggregation tools.
5. WHEN an MCP tool call completes, THE MCP_Gateway SHALL increment OpenTelemetry counters `mcp.tokens.input` and `mcp.tokens.output` (both tagged with `tool.name` and `model.tier`) and SHALL set span tags `mcp.tokens.input`, `mcp.tokens.output`, and `mcp.cost.usd` (computed as `(inputTokens × inputPricePerToken) + (outputTokens × outputPricePerToken)` using the configured per-token price for the selected model tier) on the active trace span.
6. WHEN a cached result is returned for a tool call, THE MCP_Gateway SHALL increment the `mcp.cache.hits` OpenTelemetry counter tagged with `tool.name` and SHALL NOT increment `mcp.tokens.input` or `mcp.tokens.output` for that call.
7. THE MCP_Gateway SHALL apply a fixed-window rate limiter on a per-authenticated-user basis allowing at most 50 tool calls per clock hour, queuing up to 5 additional calls per user; queued calls that are not dispatched within 30 seconds SHALL be rejected with HTTP 429.
8. IF a queued MCP tool call is not dispatched within 30 seconds of being enqueued, THEN THE MCP_Gateway SHALL return HTTP 429 with a `Retry-After` header set to the number of seconds until the current rate-limit window expires.
9. THE Reference_Repository SHALL document the monthly LLM cost estimation methodology using the §1.7 back-of-the-envelope approach applied to token volumes, including: a cost scaling table with estimated monthly cost at 10K, 100K, and 1M DAU (with and without caching), and a statement that when estimated LLM cost per DAU exceeds $0.01 per day, aggressive caching, stricter tier enforcement, or fine-tuned model deployment must be evaluated.
