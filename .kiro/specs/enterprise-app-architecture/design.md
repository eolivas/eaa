# Design Document: Enterprise Application Architecture

## Overview

This document defines the canonical architectural reference for enterprise-grade application development on this platform. It covers system design foundations, technology choices, structural conventions, and implementation patterns across all layers — from cloud infrastructure down to unit-testable domain logic.

The architecture is polyglot at the edges (React frontend, REST/MCP APIs, cloud-native services) and opinionated at the core (.NET 8+, Clean Architecture, Domain-Driven Design, SOLID principles). Every section is intended to be normative: new features and services should conform to the patterns described here unless a justified exception is documented.

The document is organized in two main parts:
- **High-Level Design** — system topology, component diagrams, technology stack, cloud infrastructure, capacity estimation
- **Low-Level Design** — code conventions, DDD building blocks, interface contracts, testing strategy, project structure, GitHub repositories and CI/CD pipelines

---

# Part 1 — High-Level Design

## Architecture

See §1.1 for the full system topology diagram, §1.2 for the Clean Architecture layer model, §1.4 for the microservices component map, §1.5 for the cloud topology (AWS and Azure), and §1.7 for capacity estimation and architectural decision gates.

## Components and Interfaces

The platform's components and their interfaces are defined across:
- §1.4 — Microservices component map with bounded contexts
- §2.3 — Domain layer interfaces (IOrderRepository, domain services)
- §2.4 — Application layer interfaces (IRequestHandler, IApplicationEventPublisher)
- §2.5 — API endpoint contracts (REST) and §2.7 — MCP tool contracts
- §2.12 — Synchronous and asynchronous inter-service interfaces

## Data Models

Core data models are defined in:
- §2.3 — DDD building blocks: Order aggregate, OrderLine entity, Money value object
- §2.10 — EF Core entity type configurations and table mappings
- §2.5 — DTOs (OrderDto, OrderLineDto) used in API responses

## Error Handling

- Domain rule violations raise typed `OrderDomainException` (extends `Exception`) and are caught by the global `ExceptionHandlingMiddleware`, which maps them to HTTP 422 Unprocessable Entity with a `ProblemDetails` body.
- Validation failures (FluentValidation in `ValidationBehaviour`) map to HTTP 400 with field-level error details.
- Infrastructure exceptions (DB timeouts, HTTP failures to downstream services) are retried via Polly `StandardResilienceHandler`; after exhausting retries they propagate as HTTP 503.
- Unhandled exceptions are caught by `UseExceptionHandler("/error")` and logged at `Critical` level with full stack trace before returning HTTP 500.

## Testing Strategy

See §2.9 for the full testing strategy including:
- Domain unit tests (pure, no mocks)
- Application handler tests (Moq-based)
- Property-based tests with FsCheck for value object invariants
- Architecture enforcement tests with NetArchTest
- Test data factories with Bogus

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system — essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Clean Architecture dependency direction is always enforced

*For any* type in the Domain assembly, it has no compile-time reference to the Application, Infrastructure, or Api assemblies; *for any* type in the Application assembly, it has no compile-time reference to the Infrastructure or Api assemblies; *for any* type in the Infrastructure assembly, it has no compile-time reference to the Api assembly.

**Validates: Requirements 1.2, 1.3, 1.4, 9.4**

### Property 2: Order creation with empty lines always throws

*For any* customer ID and empty lines collection, `Order.Create(customerId, lines)` always throws an `OrderDomainException`, so no `Order` instance can be constructed with zero `OrderLine` entries.

**Validates: Requirements 3.1, 6.2**

### Property 3: Order total is non-negative and equals sum of line totals

*For any* valid `Order`, `order.Total.Amount ≥ 0` and `order.Total.Amount` equals the sum of `line.UnitPrice.Amount × line.Quantity` for every line in `order.Lines`.

**Validates: Requirements 3.1**

### Property 4: Money value object rejects invalid construction arguments

*For any* negative amount, `Money(amount, currency)` always throws an `ArgumentException`; *for any* currency string that is not exactly three uppercase letters, `Money(amount, currency)` always throws an `ArgumentException`.

**Validates: Requirements 3.2**

### Property 5: Money addition is commutative and non-negative for non-negative inputs

*For any* two `Money` values `a` and `b` with equal currency where both amounts are ≥ 0, `(a + b).Amount == (b + a).Amount` and `(a + b).Amount ≥ 0`.

**Validates: Requirements 3.2, 9.3**

### Property 6: Order status transitions enforce the defined lifecycle

*For any* `Order` in a status other than `OrderStatus.Pending`, calling `Place()` always throws an `OrderDomainException` with a message identifying the invalid transition; *for any* `Order` in `OrderStatus.Shipped` or `OrderStatus.Cancelled`, calling `Cancel()` always throws an `OrderDomainException` with a message identifying the invalid transition; all valid transitions (`Pending → Placed`, `Placed → Shipped`, `Pending → Cancelled`, `Placed → Cancelled`) always succeed.

**Validates: Requirements 3.5, 3.6, 3.8**

### Property 7: ValidationBehaviour always prevents handler execution when validators fail

*For any* request type with a registered FluentValidation validator that produces one or more `ValidationFailure` entries, the `ValidationBehaviour` always throws a `FluentValidation.ValidationException` before the next pipeline step executes, so the handler is never invoked.

**Validates: Requirements 4.3, 4.5**

### Property 8: HTTP 400 errors always include the failing field key in the ProblemDetails errors dictionary

*For any* request body that fails FluentValidation on a named field, the Presentation_Layer always returns HTTP 400 with a `ProblemDetails` body whose `errors` dictionary contains at least one entry keyed by that field's name.

**Validates: Requirements 5.6**

### Property 9: DomainException always maps to HTTP 422 with exception message in ProblemDetails detail

*For any* `DomainException` thrown during handler execution and caught by `ExceptionHandlingMiddleware`, the response is always HTTP 422 with a `ProblemDetails` body whose `detail` field contains the exception message.

**Validates: Requirements 5.7**

### Property 10: Unhandled exceptions always produce HTTP 500 with Critical log

*For any* unhandled exception that reaches `ExceptionHandlingMiddleware`, the response is always HTTP 500 with a `ProblemDetails` body, and the exception is always logged at `Critical` level with the full stack trace.

**Validates: Requirements 5.8**

### Property 11: EfOrderRepository always returns Orders with Lines populated

*For any* `Order` retrieved via `EfOrderRepository`, `order.Lines` is never null and contains the same `OrderLine` entries that were persisted, because `.Include(o => o.Lines)` is applied on every query.

**Validates: Requirements 6.1**

### Property 12: Cached repository hit never calls the inner repository

*For any* order ID that has been previously retrieved (and therefore cached), a subsequent call to `CachedOrderRepository.GetByIdAsync` with the same ID returns the cached value and the inner `IOrderRepository` is never called.

**Validates: Requirements 6.4**

### Property 13: Discount strategies never inflate the input price

*For any* combination of `IDiscountStrategy` implementations applied via `PricingService.Calculate`, the resulting price is always ≤ the input base price.

**Validates: Requirements 6.5**

### Property 14: PendingOrdersSpecification matches exactly the set of Pending orders

*For any* `Order`, `PendingOrdersSpecification.ToExpression()` evaluates to `true` if and only if `order.Status == OrderStatus.Pending`.

**Validates: Requirements 6.6**

### Property 15: MCP get_order tool always returns not-found message for unknown IDs

*For any* UUID that does not correspond to an existing order, the `get_order` MCP tool always returns a `CallToolResult` whose `content[0].text` is a human-readable sentence of the form `"No order found with ID {orderId}."`.

**Validates: Requirements 7.3**

### Property 16: MCP tools with invalid input always return a descriptive error without exposing a stack trace

*For any* `linesJson` value that is not valid JSON or does not conform to the order-line schema, the `place_order` MCP tool always returns a `CallToolResult` whose `content[0].text` describes the validation error and never contains an exception stack trace.

**Validates: Requirements 7.5**

### Property 17: MCP DomainException always surfaces as a user-readable message, never HTTP 500

*For any* `DomainException` thrown during MCP tool handler execution, the MCP_Gateway always catches it and returns a `CallToolResult` whose `content[0].text` is the exception message rather than propagating an HTTP 500 response.

**Validates: Requirements 7.7**

### Property 18: PlaceOrderHandler always calls SaveAsync and PublishAsync exactly once per valid command

*For any* valid `PlaceOrderCommand` (an `Order` that can be created and placed without throwing), `PlaceOrderHandler.Handle` always calls `IOrderRepository.SaveAsync` exactly once and `IApplicationEventPublisher.PublishAsync` exactly once.

**Validates: Requirements 9.2**

### Property 19: EF Core round-trip preserves Order, Money, and OrderLine values

*For any* valid `Order` aggregate persisted via EF Core, a subsequent retrieve returns an `Order` with `Id` and `CustomerId` equal to the originals, a `Money` value with equal `Amount` and `Currency`, and the same `OrderLine` entries with orphan deletion enforced when a line is removed.

**Validates: Requirements 10.1, 10.2, 10.3**

### Property 20: Unprocessed OutboxMessages always have ProcessedAt IS NULL

*For any* `OutboxMessage` that has not been successfully processed by the `OutboxProcessor`, `ProcessedAt` is always null; *for any* `OutboxMessage` that has been successfully processed, `ProcessedAt` is always a non-null UTC timestamp.

**Validates: Requirements 10.5**

### Property 21: Resilience layer always returns HTTP 503 when downstream is unavailable after retries

*For any* outbound call (message bus or HTTP client) where the target is unavailable and all retries configured by the standard resilience handler are exhausted, the Infrastructure_Layer always returns an HTTP 503 response containing an error message indicating service unavailability.

**Validates: Requirements 11.4, 11.5**

### Property 22: Axios request interceptor always attaches Bearer token when token is present

*For any* outbound HTTP request sent by the axios instance in `lib/http.ts`, if a token is present in the Zustand auth store, the request always carries an `Authorization: Bearer <token>` header.

**Validates: Requirements 12.3**

### Property 23: PlaceOrderForm submit button is always disabled while mutation is pending

*For any* state of the `usePlaceOrder` mutation where `isPending === true`, the `PlaceOrderForm` submit button always has its `disabled` attribute set to `true`.

**Validates: Requirements 12.5**

### Property 24: PlaceOrderForm always renders role="alert" element on error and removes it on reset

*For any* error state of the `usePlaceOrder` mutation, the `PlaceOrderForm` always renders an element with `role="alert"` containing the error message; when the mutation is reset or re-submitted, the alert element is always removed from the DOM.

**Validates: Requirements 12.6**

### Property 25: Every endpoint group always requires authorization unless explicitly annotated AllowAnonymous

*For any* endpoint registered in the Presentation_Layer that is not explicitly annotated with `AllowAnonymous()`, the endpoint always requires authorization via `RequireAuthorization()`.

**Validates: Requirements 18.1**

### Property 26: Log entries always contain a Service field identifying the emitting service

*For any* log entry emitted by the reference service in Production, the structured log always contains a `Service` field set to the application name sourced from host configuration.

**Validates: Requirements 16.1**

### Property 27: PlaceOrderHandler always creates a PlaceOrder span with customer.id tag

*For any* invocation of `PlaceOrderHandler.Handle`, the handler always creates a child span named `"PlaceOrder"` via `ActivitySource.StartActivity` and always sets a tag with key `"customer.id"` equal to the command's `CustomerId`.

**Validates: Requirements 16.3**

---

## 1.1 System Overview

The platform is a distributed, cloud-hosted enterprise system composed of independently deployable microservices, a React single-page application, and a backend-for-frontend (BFF) layer. All services communicate over HTTP/REST or asynchronous messaging. An MCP (Model Context Protocol) gateway exposes AI-tool endpoints to authorized consumers.

```mermaid
graph TD
    subgraph Client["Client Tier"]
        SPA["React SPA\n(TypeScript + Vite)"]
    end

    subgraph Edge["Edge / Gateway Tier"]
        APIGW["API Gateway\n(AWS API Gateway / Azure APIM)"]
        BFF["BFF Service\n(.NET Minimal API)"]
        MCP["MCP Gateway\n(.NET Minimal API)"]
    end

    subgraph Services["Microservices Tier (.NET 8)"]
        SvcA["Domain Service A\n(e.g. Identity)"]
        SvcB["Domain Service B\n(e.g. Orders)"]
        SvcC["Domain Service C\n(e.g. Notifications)"]
    end

    subgraph Messaging["Async Messaging"]
        BUS["Message Bus\n(AWS SNS/SQS or Azure Service Bus)"]
    end

    subgraph Data["Data Tier"]
        DB_A["SQL DB\n(RDS / Azure SQL)"]
        DB_B["NoSQL DB\n(DynamoDB / Cosmos DB)"]
        CACHE["Distributed Cache\n(ElastiCache / Azure Cache for Redis)"]
    end

    subgraph AI["AI / MCP Tier"]
        LLM["LLM Provider\n(OpenAI / Bedrock)"]
    end

    SPA --> APIGW
    APIGW --> BFF
    APIGW --> MCP
    BFF --> SvcA
    BFF --> SvcB
    BFF --> SvcC
    SvcA --> DB_A
    SvcB --> DB_B
    SvcA --> CACHE
    SvcB --> BUS
    SvcC --> BUS
    MCP --> LLM
    MCP --> SvcA
    MCP --> SvcB
```


## 1.2 Clean Architecture Layer Model

Every microservice and the BFF follows the same four-layer Clean Architecture ring model.

```
┌─────────────────────────────────────────────────┐
│               Presentation / API                │  ← Minimal API endpoints, controllers,
│        (Infrastructure.Web / Adapters)          │    MCP tool handlers, React (frontend)
├─────────────────────────────────────────────────┤
│               Application Layer                 │  ← Use cases, command/query handlers
│           (Application project)                 │    (CQRS), DTOs, application services
├─────────────────────────────────────────────────┤
│               Domain Layer                      │  ← Aggregates, entities, value objects,
│             (Domain project)                    │    domain events, domain services,
│                                                 │    repository interfaces
├─────────────────────────────────────────────────┤
│            Infrastructure Layer                 │  ← EF Core, external HTTP clients,
│       (Infrastructure project)                  │    message producers/consumers,
│                                                 │    repository implementations, cache
└─────────────────────────────────────────────────┘

Dependency rule: outer rings depend on inner rings. The Domain layer has ZERO
external dependencies. Infrastructure depends on Domain. Application depends on
Domain. Presentation depends on Application (and optionally Infrastructure for DI).
```

## 1.3 Technology Stack

| Concern | Technology | Notes |
|---|---|---|
| Backend language | C# 12 / .NET 8 | LTS release, Minimal APIs, top-level programs |
| Frontend | React 18 + TypeScript | Vite build tooling, TanStack Query |
| ORM | Entity Framework Core 8 | Code-first migrations, owned entities for VOs |
| Messaging | MassTransit | Abstraction over SNS/SQS and Azure Service Bus |
| API style | RESTful Minimal APIs | OpenAPI via Scalar / Swashbuckle |
| AI integration | MCP SDK (.NET) | Tool registration, streaming responses |
| Containers | Docker / Docker Compose | Multi-stage builds, rootless images |
| Orchestration | Amazon ECS (Fargate) or AKS | Per-environment Helm charts / CDK stacks |
| Source control | GitHub | Monorepo or polyrepo, branch protection, CODEOWNERS |
| CI/CD | GitHub Actions | Build → test → push → deploy pipeline |
| Auth | OAuth 2.0 / OIDC | AWS Cognito or Azure Entra ID |
| Observability | OpenTelemetry | Traces, metrics, logs → AWS X-Ray / Azure Monitor |
| Testing | xUnit, Moq, FluentAssertions, Bogus | Unit + integration + architecture tests |
| Property testing | FsCheck | Generative tests for domain invariants |
| Frontend testing | Vitest + React Testing Library | Unit + component tests |


## 1.4 Microservices Component Map

```mermaid
graph LR
    subgraph "Identity Service"
        IS_API["Minimal API\n/auth, /users"]
        IS_APP["Application\nRegisterUser\nAuthenticateUser"]
        IS_DOM["Domain\nUser Aggregate\nRole Value Object"]
        IS_INF["Infrastructure\nUserRepository\nJwtTokenService"]
    end

    subgraph "Orders Service"
        OS_API["Minimal API\n/orders"]
        OS_APP["Application\nPlaceOrder\nCancelOrder"]
        OS_DOM["Domain\nOrder Aggregate\nOrderLine Entity\nMoney Value Object"]
        OS_INF["Infrastructure\nOrderRepository\nPaymentGatewayClient"]
    end

    subgraph "Notifications Service"
        NS_CON["Message Consumer\nOrderPlacedConsumer"]
        NS_APP["Application\nSendEmailNotification"]
        NS_INF["Infrastructure\nSmtpEmailSender\nSesEmailSender"]
    end

    IS_API --> IS_APP --> IS_DOM
    IS_INF --> IS_DOM
    OS_API --> OS_APP --> OS_DOM
    OS_INF --> OS_DOM
    NS_CON --> NS_APP --> NS_INF
```

## 1.5 Cloud Topology

### AWS

```
┌─── AWS Region ──────────────────────────────────────────────────────┐
│                                                                      │
│  Route 53 ──► CloudFront (React SPA, S3 origin)                     │
│                    │                                                  │
│  WAF ─────────────►│                                                  │
│                    ▼                                                  │
│             API Gateway (REST)                                        │
│                    │                                                  │
│       ┌────────────┼────────────┐                                    │
│       ▼            ▼            ▼                                    │
│   ECS Fargate  ECS Fargate  ECS Fargate                              │
│   (BFF)        (Identity)   (Orders)                                 │
│       │            │            │                                    │
│   ElastiCache   RDS (Multi-AZ) SNS/SQS ──► ECS Fargate              │
│   (Redis)       Aurora         (Bus)       (Notifications)           │
│                                                                      │
│  ECR (container images)   Secrets Manager   CloudWatch + X-Ray      │
└──────────────────────────────────────────────────────────────────────┘
```

### Azure (equivalent topology)

```
┌─── Azure Region ────────────────────────────────────────────────────┐
│                                                                      │
│  Azure Front Door ──► Static Web Apps (React SPA)                   │
│                             │                                        │
│  Azure APIM ───────────────►│                                        │
│                             ▼                                        │
│              Container Apps Environment                              │
│       ┌───────────┬──────────────┬──────────────┐                   │
│       │ BFF       │ Identity     │ Orders        │                   │
│       └───────────┴──────────────┴──────────────┘                   │
│            │             │              │                            │
│  Azure Cache for Redis  Azure SQL  Service Bus ──► Notifications    │
│                                                                      │
│  ACR (images)   Key Vault   Application Insights + Monitor          │
└──────────────────────────────────────────────────────────────────────┘
```


## 1.6 React Frontend Architecture

```mermaid
graph TD
    subgraph "React SPA"
        Router["React Router v6\n(route definitions)"]
        Pages["Pages\n(route-level components)"]
        Features["Feature Modules\n(collocated components,\nhooks, types)"]
        Shared["Shared UI Library\n(design system components)"]
        API["API Layer\n(TanStack Query +\naxios clients)"]
        State["Global State\n(Zustand for UI state)"]
        Auth["Auth Module\n(OIDC / Cognito SDK)"]
    end

    Router --> Pages --> Features
    Features --> Shared
    Features --> API
    Features --> State
    Pages --> Auth
    API --> State
```

**Folder conventions:**

```
src/
  features/
    orders/
      components/       ← feature-scoped UI components
      hooks/            ← useOrders, usePlaceOrder
      api/              ← query/mutation definitions
      types.ts          ← local TypeScript interfaces
  shared/
    components/         ← Button, Modal, DataTable …
    hooks/              ← useDebounce, usePermission …
  lib/
    http.ts             ← axios instance + interceptors
    auth.ts             ← auth helper wrappers
  app/
    router.tsx          ← route tree
    App.tsx
```

---

## 1.7 System Sizing and Capacity Estimation

Back-of-the-envelope estimation is the practice of doing rough quantitative reasoning *before* committing to an architecture decision. The goal is not precision — it is to determine whether an approach is in the right order of magnitude, which constraints are binding, and which architectural choices are justified by load rather than habit.

This section defines the estimation methodology, provides a reference numbers table, applies the method to the Orders PoC as a worked example, and maps estimation outcomes to architectural decision gates.

### 1.7.1 Estimation Methodology

Apply these steps in order for any new service or feature:

```
1. Define the load profile
   ├── Daily active users (DAU)
   ├── Peak-to-average ratio (typically 2×–10× for B2C, 1.5×–3× for B2B)
   ├── Read/write ratio (typically 80:20 to 95:5 for most services)
   └── Request size (payload bytes, typical and p99)

2. Derive QPS (queries per second)
   ├── Average QPS  = total daily requests ÷ 86,400
   ├── Peak QPS     = average QPS × peak-to-average ratio
   └── Per-endpoint QPS = peak QPS × endpoint traffic share

3. Estimate storage
   ├── Record size = sum of field sizes (estimate generously)
   ├── Daily new records = write QPS × 86,400
   ├── Annual raw storage = daily new records × record size × 365
   └── With replication = raw × replication factor (typically 3×)

4. Estimate bandwidth
   ├── Inbound  = write QPS × avg request size
   ├── Outbound = read QPS × avg response size
   └── Peak bandwidth = peak QPS × p99 payload size

5. Estimate latency budget
   ├── Target p99 end-to-end SLA (e.g., 200ms)
   ├── Subtract: network round-trip (~1ms intra-region), middleware (~5ms)
   └── Remaining budget = what the DB and business logic can spend

6. Map to architectural decisions (§1.7.4)
```

### 1.7.2 Reference Numbers

These are the standard engineering estimates used consistently across all services in this platform. Source: industry-standard system design references.

**Latency reference table**

| Operation | Approximate latency |
|---|---|
| L1 cache read | ~0.5 ns |
| L2 cache read | ~7 ns |
| RAM read | ~100 ns |
| SSD sequential read (4 KB) | ~0.1 ms |
| SSD random read | ~0.1 ms |
| HDD random read | ~10 ms |
| Redis / in-process cache read | ~0.5 ms |
| Intra-region network round-trip | ~1 ms |
| Cross-region network round-trip | ~50–150 ms |
| PostgreSQL indexed read (warm) | ~1–5 ms |
| PostgreSQL full table scan (1M rows) | ~500 ms |
| External HTTP service call (same region) | ~5–20 ms |

**Throughput and size reference table**

| Metric | Approximate value |
|---|---|
| Typical HTTP server (1 vCPU) | ~1,000–5,000 req/s |
| ECS Fargate task (2 vCPU / 4 GB) | ~2,000–8,000 req/s (.NET, simple ops) |
| PostgreSQL writes | ~1,000–5,000 writes/s (single instance) |
| PostgreSQL reads (indexed) | ~10,000–50,000 reads/s (single instance) |
| Redis throughput | ~100,000 ops/s (single node) |
| SNS/SQS message throughput | ~3,000 msgs/s (standard queue) |
| 1 GB in bytes | 10⁹ bytes |
| 1 TB in bytes | 10¹² bytes |
| Seconds in a day | 86,400 (~10⁵) |
| Seconds in a year | ~3.15 × 10⁷ (~3 × 10⁷) |

**Quick mental math shortcuts**

```
1 million requests/day  ÷ 86,400 ≈ 12 req/s average
1 billion requests/day  ÷ 86,400 ≈ 12,000 req/s average

Peak QPS rule of thumb: average × 3 (B2B), average × 10 (B2C viral)

Storage growth: 1 KB/record × 1M records/day = 1 GB/day = 365 GB/year raw
```

### 1.7.3 Worked Example — Orders Service

This is an illustrative estimation for the Orders PoC. Numbers are hypothetical and intended to demonstrate the method, not represent production targets.

**Step 1 — Load profile assumptions**

```
Daily active users (DAU)         : 100,000
Orders placed per user per day   : 0.1  (one order every 10 days on average)
Order reads per user per day     : 2.0  (checking order status, history)
Peak-to-average ratio            : 5×   (lunch-hour spike pattern)
Read/write ratio                 : 95:5 (mostly reads)
```

**Step 2 — QPS derivation**

```
Total writes/day  = 100,000 DAU × 0.1 orders/user  = 10,000 writes/day
Total reads/day   = 100,000 DAU × 2.0 reads/user   = 200,000 reads/day
Total requests/day                                  = 210,000 req/day

Average write QPS = 10,000  ÷ 86,400 ≈  0.12 req/s
Average read QPS  = 200,000 ÷ 86,400 ≈  2.3  req/s
Average total QPS                    ≈  2.4  req/s

Peak write QPS    = 0.12 × 5         ≈  0.6  req/s
Peak read QPS     = 2.3  × 5         ≈ 11.5  req/s
Peak total QPS                       ≈ 12    req/s
```

**Step 3 — Storage estimation**

```
Order record size:
  id (16 B) + customer_id (16 B) + status (8 B) + total_amount (8 B)
  + total_currency (3 B) + created_at (8 B) + overhead ≈ 200 B/order

OrderLine record size:
  id (16 B) + order_id (16 B) + product_id (16 B) + qty (4 B)
  + unit_price (8 B) + currency (3 B) + overhead ≈ 150 B/line

Avg lines per order: 3
Record size per order (with lines): 200 + (3 × 150) = 650 B ≈ 1 KB

Daily new orders   = 10,000
Daily storage      = 10,000 × 1 KB = 10 MB/day
Annual raw storage = 10 MB × 365   ≈ 3.65 GB/year
With 3× replication                ≈ 11 GB/year
With indexes (1.5×)                ≈ 16 GB/year

OutboxMessage storage:
  ~500 B/message × 10,000/day × 7-day retention = ~35 MB (negligible)
```

**Step 4 — Bandwidth estimation**

```
Inbound (writes):
  Avg request payload ≈ 512 B
  Peak write QPS ≈ 0.6 req/s
  Peak inbound bandwidth = 0.6 × 512 B ≈ 0.3 KB/s  ← negligible

Outbound (reads):
  Avg OrderDto response ≈ 2 KB
  Peak read QPS ≈ 11.5 req/s
  Peak outbound bandwidth = 11.5 × 2 KB ≈ 23 KB/s ≈ 0.2 Mbit/s  ← negligible
```

**Step 5 — Latency budget (target p99 = 200ms)**

```
Network (client → API Gateway → ECS)  :  ~5 ms
Middleware (auth, validation, logging) :  ~5 ms
MediatR pipeline overhead              :  ~1 ms
                                       --------
Remaining budget for DB + domain logic : ~189 ms

PostgreSQL indexed read (warm cache)   :  ~2 ms
PostgreSQL write + outbox commit       :  ~5 ms
Redis cache read (hit)                 :  ~1 ms
                                       --------
Comfortable margin                     : ~181 ms  ✓
```

**Step 6 — Summary**

```
┌─────────────────────────────────────────────────────────┐
│ Orders Service — Sizing Summary (100K DAU)              │
├──────────────────────┬──────────────────────────────────┤
│ Peak QPS             │ ~12 req/s total                  │
│ Annual DB storage    │ ~16 GB (with indexes + replicas) │
│ Peak bandwidth       │ ~0.2 Mbit/s outbound             │
│ p99 latency headroom │ ~181 ms remaining after overhead │
│ Compute requirement  │ 1× Fargate task (0.5 vCPU/1 GB) │
│ DB requirement       │ 1× RDS instance (db.t3.micro)    │
│ Cache requirement    │ Optional at this scale           │
└──────────────────────┴──────────────────────────────────┘
```

### 1.7.4 Architectural Decision Gates

Use estimation results to drive architecture choices consistently. These thresholds are guidelines, not hard rules — adjust based on cost, SLA, and team capacity.

| Metric | Threshold | Architectural trigger |
|---|---|---|
| Peak read QPS | > 1,000 | Add read replicas or caching layer |
| Peak read QPS | > 10,000 | Consider CQRS with separate read store (e.g., Elasticsearch, DynamoDB) |
| Peak write QPS | > 500 | Consider async write path (queue + worker) |
| Peak write QPS | > 5,000 | Consider DB sharding or event sourcing |
| Annual storage | > 1 TB | Plan partitioning, archiving, or cold storage tier |
| Annual storage | > 10 TB | Consider NoSQL (DynamoDB / Cosmos DB) over relational |
| p99 latency budget remaining | < 50 ms | Cache mandatory; review N+1 query patterns |
| p99 latency budget remaining | < 10 ms | In-process cache required; review serialization cost |
| Payload size (p99) | > 1 MB | Consider pagination, field projection, or CDN offload |
| Outbox queue depth (sustained) | > 10,000 | Scale OutboxProcessor workers or increase polling frequency |

**Decision mapping for the Orders PoC at 100K DAU:**

```
Peak QPS = 12  → single Fargate task sufficient; no read replicas needed
Storage  = 16 GB/yr → PostgreSQL on db.t3.micro sufficient; no sharding
Latency  = 181 ms headroom → cache is optional; Redis deferred until > 1K QPS
Bandwidth = negligible → no CDN optimization required for API responses
```

### 1.7.5 Scaling Projection

When applying this pattern to downstream projects, estimate three scenarios:

```
Scenario A (launch)     : Current expected DAU at go-live
Scenario B (12 months)  : Expected DAU after first year (typically 3×–10× launch)
Scenario C (worst case) : Viral / maximum credible load (100× launch)
```

If Scenario C crosses any decision gate threshold, the architecture must handle that scale from day one (or have a documented, tested migration path to reach it within the expected timeframe).

---

# Part 2 — Low-Level Design

## 2.1 .NET Solution and Project Structure

Each microservice follows the same solution layout. The service name prefix (e.g. `Orders`) is replaced per service.

```
Orders.sln
│
├── src/
│   ├── Orders.Domain/              ← Class library — no external NuGet deps
│   │   ├── Aggregates/
│   │   │   ├── Order.cs
│   │   │   └── OrderLine.cs
│   │   ├── ValueObjects/
│   │   │   ├── Money.cs
│   │   │   └── OrderStatus.cs
│   │   ├── Events/
│   │   │   └── OrderPlacedEvent.cs
│   │   ├── Repositories/
│   │   │   └── IOrderRepository.cs
│   │   ├── Services/
│   │   │   └── IPricingService.cs
│   │   └── Exceptions/
│   │       └── OrderDomainException.cs
│   │
│   ├── Orders.Application/         ← References Domain only
│   │   ├── Commands/
│   │   │   ├── PlaceOrder/
│   │   │   │   ├── PlaceOrderCommand.cs
│   │   │   │   └── PlaceOrderHandler.cs
│   │   │   └── CancelOrder/
│   │   │       ├── CancelOrderCommand.cs
│   │   │       └── CancelOrderHandler.cs
│   │   ├── Queries/
│   │   │   └── GetOrder/
│   │   │       ├── GetOrderQuery.cs
│   │   │       └── GetOrderHandler.cs
│   │   ├── DTOs/
│   │   │   └── OrderDto.cs
│   │   └── Abstractions/
│   │       └── IApplicationEventPublisher.cs
│   │
│   ├── Orders.Infrastructure/      ← References Domain + Application
│   │   ├── Persistence/
│   │   │   ├── OrdersDbContext.cs
│   │   │   ├── Repositories/
│   │   │   │   └── OrderRepository.cs
│   │   │   └── Configurations/
│   │   │       └── OrderEntityTypeConfiguration.cs
│   │   ├── Messaging/
│   │   │   └── MassTransitEventPublisher.cs
│   │   └── ExternalServices/
│   │       └── PaymentGatewayClient.cs
│   │
│   └── Orders.Api/                 ← References Application + Infrastructure (DI only)
│       ├── Endpoints/
│       │   └── OrdersEndpoints.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       └── Program.cs
│
└── tests/
    ├── Orders.Domain.Tests/
    ├── Orders.Application.Tests/
    ├── Orders.Infrastructure.Tests/
    ├── Orders.Api.Tests/
    └── Orders.Architecture.Tests/  ← NetArchTest rules
```


## 2.2 SOLID Principles — Reference Implementations

### Single Responsibility Principle (SRP)

Each class has one reason to change. Command handlers are responsible only for orchestrating a single use case.

```csharp
// WRONG — handler also formats and sends email
public class PlaceOrderHandler
{
    public async Task Handle(PlaceOrderCommand cmd)
    {
        var order = Order.Create(cmd.CustomerId, cmd.Lines);
        await _repo.SaveAsync(order);
        // SRP violation: email concern embedded here
        var html = $"<h1>Order {order.Id} placed</h1>";
        await _smtp.SendAsync(cmd.Email, html);
    }
}

// CORRECT — handler publishes domain event; email is a side-effect handled elsewhere
public class PlaceOrderHandler : IRequestHandler<PlaceOrderCommand, Guid>
{
    private readonly IOrderRepository _repo;
    private readonly IApplicationEventPublisher _events;

    public PlaceOrderHandler(IOrderRepository repo, IApplicationEventPublisher events)
    {
        _repo = repo;
        _events = events;
    }

    public async Task<Guid> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var order = Order.Create(cmd.CustomerId, cmd.Lines);
        await _repo.SaveAsync(order, ct);
        await _events.PublishAsync(new OrderPlacedEvent(order.Id), ct);
        return order.Id;
    }
}
```

### Open/Closed Principle (OCP)

New discount strategies are added by implementing an interface, not modifying existing code.

```csharp
public interface IDiscountStrategy
{
    Money Apply(Money price, Order order);
}

public class SeasonalDiscountStrategy : IDiscountStrategy
{
    public Money Apply(Money price, Order order) =>
        price * 0.9m; // 10% off
}

public class LoyaltyDiscountStrategy : IDiscountStrategy
{
    public Money Apply(Money price, Order order) =>
        order.CustomerId.IsLoyal() ? price * 0.85m : price;
}

// PricingService is closed for modification, open for extension
public class PricingService
{
    private readonly IEnumerable<IDiscountStrategy> _strategies;
    public PricingService(IEnumerable<IDiscountStrategy> strategies) =>
        _strategies = strategies;

    public Money Calculate(Money basePrice, Order order) =>
        _strategies.Aggregate(basePrice, (price, s) => s.Apply(price, order));
}
```

### Liskov Substitution Principle (LSP)

Subtypes must be substitutable for their base types without altering correctness.

```csharp
// All IOrderRepository implementations must honour the contract:
// - SaveAsync must persist the aggregate and raise no domain exceptions on valid input
// - GetByIdAsync must return null (not throw) when an order does not exist
public interface IOrderRepository
{
    Task SaveAsync(Order order, CancellationToken ct = default);
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct = default);
}
```

### Interface Segregation Principle (ISP)

Clients depend only on the methods they use.

```csharp
// Fat interface — violates ISP
public interface IOrderService
{
    Task PlaceOrder(PlaceOrderCommand cmd);
    Task CancelOrder(CancelOrderCommand cmd);
    Task<OrderDto> GetOrder(Guid id);
    Task<byte[]> ExportToPdf(Guid id);    // only reporting consumers need this
}

// Segregated interfaces
public interface IOrderWriter
{
    Task PlaceOrder(PlaceOrderCommand cmd);
    Task CancelOrder(CancelOrderCommand cmd);
}

public interface IOrderReader
{
    Task<OrderDto> GetOrder(Guid id);
}

public interface IOrderExporter
{
    Task<byte[]> ExportToPdf(Guid id);
}
```

### Dependency Inversion Principle (DIP)

High-level modules depend on abstractions; infrastructure registers concrete implementations.

```csharp
// Domain layer — defines abstraction
public interface IOrderRepository { /* ... */ }

// Infrastructure layer — provides concrete implementation
public class EfOrderRepository : IOrderRepository { /* ... */ }

// Program.cs — wires everything via DI
builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<IApplicationEventPublisher, MassTransitEventPublisher>();
```


## 2.3 Domain-Driven Design Building Blocks

### Aggregate Root

```csharp
// Orders.Domain/Aggregates/Order.cs
public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public CustomerId CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money Total { get; private set; }

    // Private constructor enforces factory method usage
    private Order() { }

    public static Order Create(CustomerId customerId, IEnumerable<OrderLineRequest> lines)
    {
        ArgumentNullException.ThrowIfNull(customerId);
        if (!lines.Any()) throw new OrderDomainException("An order must have at least one line.");

        var order = new Order
        {
            Id = OrderId.New(),
            CustomerId = customerId,
            Status = OrderStatus.Pending
        };

        foreach (var l in lines)
            order.AddLine(l.ProductId, l.Quantity, l.UnitPrice);

        order.RaiseDomainEvent(new OrderCreatedEvent(order.Id, customerId));
        return order;
    }

    public void Place()
    {
        if (Status != OrderStatus.Pending)
            throw new OrderDomainException($"Cannot place order in status {Status}.");

        Status = OrderStatus.Placed;
        RaiseDomainEvent(new OrderPlacedEvent(Id, CustomerId, Total));
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Shipped)
            throw new OrderDomainException("Cannot cancel a shipped order.");

        Status = OrderStatus.Cancelled;
        RaiseDomainEvent(new OrderCancelledEvent(Id, reason));
    }

    private void AddLine(ProductId productId, int qty, Money unitPrice)
    {
        var line = OrderLine.Create(productId, qty, unitPrice);
        _lines.Add(line);
        Total = _lines.Sum(l => l.LineTotal);
    }
}
```

### Entity

```csharp
// Orders.Domain/Aggregates/OrderLine.cs
public sealed class OrderLine : Entity<OrderLineId>
{
    public ProductId ProductId { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money LineTotal => UnitPrice * Quantity;

    private OrderLine() { }

    internal static OrderLine Create(ProductId productId, int qty, Money unitPrice)
    {
        if (qty <= 0) throw new OrderDomainException("Quantity must be positive.");
        return new OrderLine
        {
            Id = OrderLineId.New(),
            ProductId = productId,
            Quantity = qty,
            UnitPrice = unitPrice
        };
    }
}
```

### Value Object

```csharp
// Orders.Domain/ValueObjects/Money.cs
public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0) throw new ArgumentException("Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.");

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0, currency);

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add money of different currencies.");
        return new Money(Amount + other.Amount, Currency);
    }

    public static Money operator +(Money a, Money b) => a.Add(b);
    public static Money operator *(Money m, int qty) => new(m.Amount * qty, m.Currency);
    public static Money operator *(Money m, decimal factor) => new(Math.Round(m.Amount * factor, 2), m.Currency);

    public override string ToString() => $"{Amount:F2} {Currency}";
}
```

### Repository Interface

```csharp
// Orders.Domain/Repositories/IOrderRepository.cs
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByCustomerAsync(CustomerId customerId, CancellationToken ct = default);
    Task SaveAsync(Order order, CancellationToken ct = default);
    Task DeleteAsync(OrderId id, CancellationToken ct = default);
}
```

### Domain Events

```csharp
// Orders.Domain/Events/
public abstract record DomainEvent(Guid EventId, DateTimeOffset OccurredAt)
{
    protected DomainEvent() : this(Guid.NewGuid(), DateTimeOffset.UtcNow) { }
}

public sealed record OrderCreatedEvent(OrderId OrderId, CustomerId CustomerId) : DomainEvent;
public sealed record OrderPlacedEvent(OrderId OrderId, CustomerId CustomerId, Money Total) : DomainEvent;
public sealed record OrderCancelledEvent(OrderId OrderId, string Reason) : DomainEvent;
```

### Aggregate Root Base

```csharp
// Orders.Domain/Primitives/AggregateRoot.cs
public abstract class AggregateRoot<TId> : Entity<TId>
{
    private readonly List<DomainEvent> _domainEvents = new();
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(DomainEvent @event) => _domainEvents.Add(@event);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```


## 2.4 Application Layer — CQRS with MediatR

### Command and Handler

```csharp
// PlaceOrderCommand.cs
public sealed record PlaceOrderCommand(
    Guid CustomerId,
    IReadOnlyList<OrderLineRequest> Lines
) : IRequest<Guid>;

public sealed record OrderLineRequest(Guid ProductId, int Quantity, decimal UnitPrice, string Currency);

// PlaceOrderHandler.cs
public sealed class PlaceOrderHandler : IRequestHandler<PlaceOrderCommand, Guid>
{
    private readonly IOrderRepository _repo;
    private readonly IApplicationEventPublisher _publisher;

    public PlaceOrderHandler(IOrderRepository repo, IApplicationEventPublisher publisher)
    {
        _repo = repo;
        _publisher = publisher;
    }

    public async Task<Guid> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var lines = cmd.Lines.Select(l =>
            new OrderLineRequest(l.ProductId, l.Quantity, new Money(l.UnitPrice, l.Currency)));

        var order = Order.Create(new CustomerId(cmd.CustomerId), lines);
        order.Place();

        await _repo.SaveAsync(order, ct);

        foreach (var evt in order.DomainEvents)
            await _publisher.PublishAsync(evt, ct);

        order.ClearDomainEvents();
        return order.Id.Value;
    }
}
```

### Query and Handler

```csharp
// GetOrderQuery.cs
public sealed record GetOrderQuery(Guid OrderId) : IRequest<OrderDto?>;

// GetOrderHandler.cs
public sealed class GetOrderHandler : IRequestHandler<GetOrderQuery, OrderDto?>
{
    private readonly IOrderRepository _repo;

    public GetOrderHandler(IOrderRepository repo) => _repo = repo;

    public async Task<OrderDto?> Handle(GetOrderQuery query, CancellationToken ct)
    {
        var order = await _repo.GetByIdAsync(new OrderId(query.OrderId), ct);
        return order is null ? null : OrderDto.From(order);
    }
}
```

### Pipeline Behaviours (Cross-Cutting Concerns)

```csharp
// ValidationBehaviour.cs — runs FluentValidation before every command handler
public sealed class ValidationBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators) =>
        _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        var context = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}

// LoggingBehaviour.cs
public sealed class LoggingBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger) =>
        _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        _logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {RequestName}", typeof(TRequest).Name);
        return response;
    }
}
```


## 2.5 Minimal API Endpoints and RESTful Contracts

### Endpoint Definition

```csharp
// Orders.Api/Endpoints/OrdersEndpoints.cs
public static class OrdersEndpoints
{
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders")
            .WithTags("Orders")
            .RequireAuthorization()
            .WithOpenApi();

        group.MapPost("/", PlaceOrder)
            .Produces<Guid>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .WithSummary("Place a new order");

        group.MapGet("/{id:guid}", GetOrder)
            .Produces<OrderDto>()
            .Produces(StatusCodes.Status404NotFound)
            .WithSummary("Get order by ID");

        group.MapDelete("/{id:guid}", CancelOrder)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status409Conflict)
            .WithSummary("Cancel an order");

        return app;
    }

    private static async Task<IResult> PlaceOrder(
        PlaceOrderRequest request,
        ISender mediator,
        CancellationToken ct)
    {
        var id = await mediator.Send(new PlaceOrderCommand(request.CustomerId, request.Lines), ct);
        return Results.Created($"/api/orders/{id}", id);
    }

    private static async Task<IResult> GetOrder(
        Guid id,
        ISender mediator,
        CancellationToken ct)
    {
        var dto = await mediator.Send(new GetOrderQuery(id), ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> CancelOrder(
        Guid id,
        CancelOrderRequest request,
        ISender mediator,
        CancellationToken ct)
    {
        await mediator.Send(new CancelOrderCommand(id, request.Reason), ct);
        return Results.NoContent();
    }
}
```

### REST API Contract (OpenAPI summary)

```
POST   /api/orders
  Body:   { "customerId": "uuid", "lines": [{ "productId": "uuid", "quantity": 2, "unitPrice": 9.99, "currency": "USD" }] }
  201:    "uuid"  (order ID)
  400:    ProblemDetails (validation errors)
  401:    Unauthorized
  
GET    /api/orders/{id}
  200:    OrderDto
  401:    Unauthorized
  404:    Not Found

DELETE /api/orders/{id}
  Body:   { "reason": "string" }
  204:    No Content
  401:    Unauthorized
  409:    Conflict (order in non-cancellable state)
```

### DTO

```csharp
public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal TotalAmount,
    string TotalCurrency,
    IReadOnlyList<OrderLineDto> Lines,
    DateTimeOffset CreatedAt)
{
    public static OrderDto From(Order order) => new(
        order.Id.Value,
        order.CustomerId.Value,
        order.Status.ToString(),
        order.Total.Amount,
        order.Total.Currency,
        order.Lines.Select(OrderLineDto.From).ToList(),
        order.CreatedAt);
}

public sealed record OrderLineDto(Guid ProductId, int Quantity, decimal UnitPrice, string Currency)
{
    public static OrderLineDto From(OrderLine l) =>
        new(l.ProductId.Value, l.Quantity, l.UnitPrice.Amount, l.UnitPrice.Currency);
}
```


## 2.6 Design Patterns Reference

### Repository Pattern

Already shown in §2.3. Infrastructure implementation:

```csharp
// Orders.Infrastructure/Persistence/Repositories/OrderRepository.cs
public sealed class EfOrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _db;
    public EfOrderRepository(OrdersDbContext db) => _db = db;

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct) =>
        await _db.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        if (_db.Entry(order).State == EntityState.Detached)
            _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(OrderId id, CancellationToken ct)
    {
        var order = await GetByIdAsync(id, ct);
        if (order is not null) _db.Orders.Remove(order);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(CustomerId customerId, CancellationToken ct) =>
        await _db.Orders
            .Where(o => o.CustomerId == customerId)
            .Include(o => o.Lines)
            .ToListAsync(ct);
}
```

### Factory Method / Static Factory on Aggregates

The `Order.Create(...)` and `OrderLine.Create(...)` methods shown in §2.3 implement the Factory pattern, ensuring all invariants are enforced at construction time and preventing invalid state from ever being represented.

### Outbox Pattern (Reliable Messaging)

Prevents lost domain events when the service crashes between saving to DB and publishing to the bus.

```csharp
// 1. During SaveChangesAsync, serialise domain events to an OutboxMessage table (same transaction)
public sealed class OrdersDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var domainEvents = ChangeTracker.Entries<AggregateRoot<OrderId>>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        foreach (var evt in domainEvents)
        {
            OutboxMessages.Add(new OutboxMessage(
                Guid.NewGuid(),
                evt.GetType().AssemblyQualifiedName!,
                JsonSerializer.Serialize(evt, evt.GetType()),
                DateTimeOffset.UtcNow));
        }

        var result = await base.SaveChangesAsync(ct);

        // Clear events after persisting
        ChangeTracker.Entries<AggregateRoot<OrderId>>()
            .ToList()
            .ForEach(e => e.Entity.ClearDomainEvents());

        return result;
    }
}

// 2. Background worker polls OutboxMessages and publishes them, then marks as processed
public sealed class OutboxProcessor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await ProcessPendingMessagesAsync(ct);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }
    // ... fetch unprocessed rows, deserialise, publish via MassTransit, mark processed
}
```

### Decorator Pattern (for cross-cutting concerns)

```csharp
// Cached decorator for IOrderRepository
public sealed class CachedOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _inner;
    private readonly IDistributedCache _cache;

    public CachedOrderRepository(IOrderRepository inner, IDistributedCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken ct)
    {
        var key = $"order:{id.Value}";
        var cached = await _cache.GetStringAsync(key, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<Order>(cached);

        var order = await _inner.GetByIdAsync(id, ct);
        if (order is not null)
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(order),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }, ct);

        return order;
    }

    public Task SaveAsync(Order order, CancellationToken ct) => _inner.SaveAsync(order, ct);
    public Task DeleteAsync(OrderId id, CancellationToken ct) => _inner.DeleteAsync(id, ct);
    public Task<IReadOnlyList<Order>> GetByCustomerAsync(CustomerId cid, CancellationToken ct) =>
        _inner.GetByCustomerAsync(cid, ct);
}
```

### Strategy Pattern

Already shown in §2.2 (discount strategies). Registered via DI:

```csharp
builder.Services.AddScoped<IDiscountStrategy, SeasonalDiscountStrategy>();
builder.Services.AddScoped<IDiscountStrategy, LoyaltyDiscountStrategy>();
builder.Services.AddScoped<PricingService>();
```

### Specification Pattern

```csharp
public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();
    public bool IsSatisfiedBy(T entity) => ToExpression().Compile()(entity);
}

public sealed class PendingOrdersSpecification : Specification<Order>
{
    public override Expression<Func<Order, bool>> ToExpression() =>
        o => o.Status == OrderStatus.Pending;
}
```


## 2.7 MCP (Model Context Protocol) Integration

The MCP gateway exposes structured AI-callable tools backed by application layer services.

```mermaid
sequenceDiagram
    participant LLM as LLM / AI Agent
    participant MCP as MCP Gateway (.NET)
    participant App as Orders Application
    participant DB  as Database

    LLM->>MCP: tools/call { name: "get_order", arguments: { orderId } }
    MCP->>App: mediator.Send(GetOrderQuery)
    App->>DB: SELECT ...
    DB-->>App: Order record
    App-->>MCP: OrderDto
    MCP-->>LLM: CallToolResult { content: [ { type: "text", text: "Order JSON" } ] }
```

### Tool Registration

```csharp
// Orders.Api/Program.cs
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<OrderMcpTools>();

// Orders.Api/Mcp/OrderMcpTools.cs
[McpServerToolType]
public sealed class OrderMcpTools
{
    private readonly ISender _mediator;
    public OrderMcpTools(ISender mediator) => _mediator = mediator;

    [McpServerTool(Name = "get_order", Description = "Retrieves an order by its ID.")]
    public async Task<string> GetOrder(
        [McpServerToolParameter(Description = "The GUID of the order to retrieve.")] Guid orderId,
        CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetOrderQuery(orderId), ct);
        return dto is null
            ? $"No order found with ID {orderId}."
            : JsonSerializer.Serialize(dto);
    }

    [McpServerTool(Name = "place_order", Description = "Places a new order for a customer.")]
    public async Task<string> PlaceOrder(
        [McpServerToolParameter(Description = "Customer GUID")] Guid customerId,
        [McpServerToolParameter(Description = "JSON array of order lines")] string linesJson,
        CancellationToken ct)
    {
        var lines = JsonSerializer.Deserialize<List<OrderLineRequest>>(linesJson)!;
        var id = await _mediator.Send(new PlaceOrderCommand(customerId, lines), ct);
        return $"Order placed successfully. Order ID: {id}";
    }
}
```

### MCP Tool Manifest (JSON)

```json
{
  "tools": [
    {
      "name": "get_order",
      "description": "Retrieves an order by its ID.",
      "inputSchema": {
        "type": "object",
        "properties": {
          "orderId": { "type": "string", "format": "uuid" }
        },
        "required": ["orderId"]
      }
    },
    {
      "name": "place_order",
      "description": "Places a new order for a customer.",
      "inputSchema": {
        "type": "object",
        "properties": {
          "customerId": { "type": "string", "format": "uuid" },
          "linesJson": { "type": "string", "description": "JSON array of order line objects" }
        },
        "required": ["customerId", "linesJson"]
      }
    }
  ]
}
```


## 2.8 Docker Containerization

### Multi-Stage Dockerfile (per .NET service)

```dockerfile
# syntax=docker/dockerfile:1

# ── Stage 1: Restore ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src
COPY ["src/Orders.Api/Orders.Api.csproj",            "Orders.Api/"]
COPY ["src/Orders.Application/Orders.Application.csproj", "Orders.Application/"]
COPY ["src/Orders.Domain/Orders.Domain.csproj",       "Orders.Domain/"]
COPY ["src/Orders.Infrastructure/Orders.Infrastructure.csproj", "Orders.Infrastructure/"]
RUN dotnet restore "Orders.Api/Orders.Api.csproj"

# ── Stage 2: Build ─────────────────────────────────────────────────────
FROM restore AS build
COPY src/ .
RUN dotnet build "Orders.Api/Orders.Api.csproj" -c Release --no-restore

# ── Stage 3: Publish ───────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "Orders.Api/Orders.Api.csproj" -c Release --no-build -o /app/publish

# ── Stage 4: Runtime ───────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Run as non-root
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Orders.Api.dll"]
```

### Docker Compose (local development)

```yaml
# docker-compose.yml
services:
  orders-api:
    build:
      context: .
      dockerfile: src/Orders.Api/Dockerfile
    ports:
      - "5001:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__Orders=Host=postgres;Database=orders;Username=orders;Password=orders
      - Messaging__Host=rabbitmq://guest:guest@rabbitmq:5672/
    depends_on:
      postgres:
        condition: service_healthy
      rabbitmq:
        condition: service_healthy

  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: orders
      POSTGRES_USER: orders
      POSTGRES_PASSWORD: orders
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U orders"]
      interval: 5s
      timeout: 5s
      retries: 5

  rabbitmq:
    image: rabbitmq:3.13-management-alpine
    ports:
      - "5672:5672"
      - "15672:15672"
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5

  react-app:
    build:
      context: frontend
      dockerfile: Dockerfile
    ports:
      - "3000:80"
```

### React Dockerfile

```dockerfile
# syntax=docker/dockerfile:1
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci --frozen-lockfile
COPY . .
RUN npm run build

FROM nginx:1.25-alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```


## 2.9 Unit Testing Strategy

All tests follow the Arrange-Act-Assert (AAA) pattern. Tests are grouped by layer with distinct responsibilities.

### Domain Tests — Pure Logic, No Mocks

```csharp
// Orders.Domain.Tests/OrderTests.cs
public sealed class OrderTests
{
    [Fact]
    public void Create_WithValidInputs_ReturnsOrderInPendingStatus()
    {
        // Arrange
        var customerId = CustomerId.New();
        var lines = new[] { new OrderLineRequest(ProductId.New(), 2, new Money(10m, "USD")) };

        // Act
        var order = Order.Create(customerId, lines);

        // Assert
        order.Status.Should().Be(OrderStatus.Pending);
        order.Lines.Should().HaveCount(1);
        order.Total.Amount.Should().Be(20m);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCreatedEvent);
    }

    [Fact]
    public void Place_WhenPending_TransitionsToPlacedAndRaisesEvent()
    {
        // Arrange
        var order = OrderFaker.CreatePendingOrder();

        // Act
        order.Place();

        // Assert
        order.Status.Should().Be(OrderStatus.Placed);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPlacedEvent);
    }

    [Fact]
    public void Place_WhenAlreadyPlaced_ThrowsDomainException()
    {
        // Arrange
        var order = OrderFaker.CreatePlacedOrder();

        // Act
        var act = () => order.Place();

        // Assert
        act.Should().Throw<OrderDomainException>()
            .WithMessage("*Cannot place order*");
    }

    [Fact]
    public void Create_WithNoLines_ThrowsDomainException()
    {
        // Arrange
        var customerId = CustomerId.New();
        var lines = Array.Empty<OrderLineRequest>();

        // Act & Assert
        FluentActions.Invoking(() => Order.Create(customerId, lines))
            .Should().Throw<OrderDomainException>()
            .WithMessage("*at least one line*");
    }
}
```

### Application Tests — Handler Tests with Mocked Dependencies

```csharp
// Orders.Application.Tests/PlaceOrderHandlerTests.cs
public sealed class PlaceOrderHandlerTests
{
    private readonly Mock<IOrderRepository> _repoMock = new();
    private readonly Mock<IApplicationEventPublisher> _publisherMock = new();
    private readonly PlaceOrderHandler _sut;

    public PlaceOrderHandlerTests()
    {
        _sut = new PlaceOrderHandler(_repoMock.Object, _publisherMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesOrderAndPublishesEvent()
    {
        // Arrange
        var cmd = new PlaceOrderCommand(
            Guid.NewGuid(),
            [new OrderLineRequest(Guid.NewGuid(), 1, 50m, "USD")]);

        // Act
        var orderId = await _sut.Handle(cmd, CancellationToken.None);

        // Assert
        orderId.Should().NotBeEmpty();
        _repoMock.Verify(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once);
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<OrderPlacedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

### Value Object Property Tests (FsCheck)

```csharp
// Orders.Domain.Tests/MoneyPropertyTests.cs
public sealed class MoneyPropertyTests
{
    [Property]
    public Property Addition_IsCommutative()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 100000).Select(x => new Money(x, "USD"))),
            Arb.From(Gen.Choose(0, 100000).Select(x => new Money(x, "USD"))),
            (a, b) => (a + b).Amount == (b + a).Amount);
    }

    [Property]
    public Property Addition_PreservesNonNegativity()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(0, 100000).Select(x => new Money(x, "USD"))),
            Arb.From(Gen.Choose(0, 100000).Select(x => new Money(x, "USD"))),
            (a, b) => (a + b).Amount >= 0);
    }
}
```

### Architecture Tests (NetArchTest)

```csharp
// Orders.Architecture.Tests/ArchitectureTests.cs
public sealed class ArchitectureTests
{
    private const string DomainNamespace      = "Orders.Domain";
    private const string ApplicationNamespace = "Orders.Application";
    private const string InfraNamespace       = "Orders.Infrastructure";
    private const string ApiNamespace         = "Orders.Api";

    [Fact]
    public void DomainLayer_ShouldNotReferenceInfrastructure()
    {
        Types.InAssembly(typeof(Order).Assembly)
            .Should().NotHaveDependencyOn(InfraNamespace)
            .GetResult().IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainLayer_ShouldNotReferenceApplication()
    {
        Types.InAssembly(typeof(Order).Assembly)
            .Should().NotHaveDependencyOn(ApplicationNamespace)
            .GetResult().IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ApplicationLayer_ShouldNotReferenceInfrastructure()
    {
        Types.InAssembly(typeof(PlaceOrderCommand).Assembly)
            .Should().NotHaveDependencyOn(InfraNamespace)
            .GetResult().IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Handlers_ShouldBeSealed()
    {
        Types.InAssembly(typeof(PlaceOrderHandler).Assembly)
            .That().ImplementInterface(typeof(IRequestHandler<,>))
            .Should().BeSealed()
            .GetResult().IsSuccessful.Should().BeTrue();
    }
}
```

### Test Data Builders with Bogus

```csharp
// Tests/_Shared/OrderFaker.cs
public static class OrderFaker
{
    private static readonly Faker F = new();

    public static Order CreatePendingOrder(int lineCount = 2)
    {
        var customerId = CustomerId.New();
        var lines = Enumerable.Range(0, lineCount).Select(_ =>
            new OrderLineRequest(
                ProductId.New(),
                F.Random.Int(1, 10),
                new Money(F.Random.Decimal(1, 500), "USD")));
        return Order.Create(customerId, lines);
    }

    public static Order CreatePlacedOrder()
    {
        var order = CreatePendingOrder();
        order.Place();
        order.ClearDomainEvents();
        return order;
    }
}
```


## 2.10 Infrastructure Layer — EF Core Configuration

```csharp
// Orders.Infrastructure/Persistence/OrdersDbContext.cs
public sealed class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
    }
}

// Orders.Infrastructure/Persistence/Configurations/OrderEntityTypeConfiguration.cs
public sealed class OrderEntityTypeConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasConversion(id => id.Value, value => new OrderId(value))
            .HasColumnName("id");

        builder.Property(o => o.CustomerId)
            .HasConversion(c => c.Value, v => new CustomerId(v))
            .HasColumnName("customer_id");

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasColumnName("status");

        // Value object mapped as owned entity (two columns: total_amount, total_currency)
        builder.OwnsOne(o => o.Total, m =>
        {
            m.Property(x => x.Amount).HasColumnName("total_amount");
            m.Property(x => x.Currency).HasColumnName("total_currency");
        });

        builder.OwnsMany(o => o.Lines, line =>
        {
            line.ToTable("order_lines");
            line.HasKey(l => l.Id);
            line.Property(l => l.Id)
                .HasConversion(id => id.Value, v => new OrderLineId(v));
            line.Property(l => l.Quantity).HasColumnName("quantity");
            line.OwnsOne(l => l.UnitPrice, m =>
            {
                m.Property(x => x.Amount).HasColumnName("unit_price_amount");
                m.Property(x => x.Currency).HasColumnName("unit_price_currency");
            });
        });

        builder.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

## 2.11 Program.cs Bootstrap

```csharp
// Orders.Api/Program.cs
var builder = WebApplication.CreateBuilder(args);

// ── Application services ──────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(PlaceOrderCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
});

builder.Services.AddValidatorsFromAssembly(typeof(PlaceOrderCommand).Assembly);

// ── Infrastructure ────────────────────────────────────────────────────
builder.Services.AddDbContext<OrdersDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("Orders")));

builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<IApplicationEventPublisher, MassTransitEventPublisher>();
builder.Services.AddScoped<IDiscountStrategy, SeasonalDiscountStrategy>();
builder.Services.AddScoped<IDiscountStrategy, LoyaltyDiscountStrategy>();

// ── Messaging (MassTransit) ───────────────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["Messaging:Host"]);
        cfg.ConfigureEndpoints(ctx);
    });
});

// ── MCP ───────────────────────────────────────────────────────────────
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<OrderMcpTools>();

// ── Auth ──────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.Authority = builder.Configuration["Auth:Authority"];
        opts.Audience  = builder.Configuration["Auth:Audience"];
    });
builder.Services.AddAuthorization();

// ── OpenAPI ───────────────────────────────────────────────────────────
builder.Services.AddOpenApi();

// ── Observability ─────────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddEntityFrameworkCoreInstrumentation())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation());

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────
app.UseExceptionHandler("/error");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapOrdersEndpoints();
app.MapMcp("/mcp");

await app.RunAsync();
```


## 2.12 Microservices Communication Patterns

### Synchronous — HTTP (service-to-service via typed client)

```csharp
// Registration
builder.Services.AddHttpClient<IInventoryClient, InventoryHttpClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:Inventory"]!);
})
.AddStandardResilienceHandler(); // Polly-based retry + circuit breaker

// Client abstraction
public interface IInventoryClient
{
    Task<bool> IsInStockAsync(Guid productId, int quantity, CancellationToken ct = default);
}

public sealed class InventoryHttpClient : IInventoryClient
{
    private readonly HttpClient _http;
    public InventoryHttpClient(HttpClient http) => _http = http;

    public async Task<bool> IsInStockAsync(Guid productId, int quantity, CancellationToken ct)
    {
        var result = await _http.GetFromJsonAsync<StockCheckResult>(
            $"/api/inventory/{productId}/stock?qty={quantity}", ct);
        return result?.IsAvailable ?? false;
    }
}
```

### Asynchronous — Domain Events via MassTransit

```csharp
// Publisher side (Orders service) — wrapped by MassTransitEventPublisher
public sealed class MassTransitEventPublisher : IApplicationEventPublisher
{
    private readonly IPublishEndpoint _endpoint;
    public MassTransitEventPublisher(IPublishEndpoint endpoint) => _endpoint = endpoint;

    public async Task PublishAsync(DomainEvent @event, CancellationToken ct) =>
        await _endpoint.Publish(@event, @event.GetType(), ct);
}

// Consumer side (Notifications service)
public sealed class OrderPlacedConsumer : IConsumer<OrderPlacedEvent>
{
    private readonly IEmailSender _emailSender;
    public OrderPlacedConsumer(IEmailSender emailSender) => _emailSender = emailSender;

    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var evt = context.Message;
        await _emailSender.SendAsync(
            subject: $"Order {evt.OrderId} confirmed",
            body: $"Your order total: {evt.Total}",
            cancellationToken: context.CancellationToken);
    }
}
```

## 2.13 React Frontend — Key Patterns

### TanStack Query Integration

```typescript
// features/orders/api/ordersApi.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { http } from '@/lib/http';
import type { Order, PlaceOrderRequest } from '../types';

export const orderKeys = {
  all:    ['orders'] as const,
  detail: (id: string) => ['orders', id] as const,
};

export function useOrder(id: string) {
  return useQuery({
    queryKey: orderKeys.detail(id),
    queryFn: () => http.get<Order>(`/api/orders/${id}`).then(r => r.data),
    enabled: !!id,
  });
}

export function usePlaceOrder() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (req: PlaceOrderRequest) =>
      http.post<string>('/api/orders', req).then(r => r.data),
    onSuccess: () => qc.invalidateQueries({ queryKey: orderKeys.all }),
  });
}
```

### Feature Component

```typescript
// features/orders/components/PlaceOrderForm.tsx
import { usePlaceOrder } from '../api/ordersApi';
import type { PlaceOrderRequest } from '../types';

export function PlaceOrderForm() {
  const { mutate, isPending, isError, error } = usePlaceOrder();

  const handleSubmit = (data: PlaceOrderRequest) => mutate(data);

  return (
    <form onSubmit={/* react-hook-form handleSubmit(handleSubmit) */}>
      {/* form fields */}
      {isError && <p role="alert">{String(error)}</p>}
      <button type="submit" disabled={isPending}>
        {isPending ? 'Placing…' : 'Place Order'}
      </button>
    </form>
  );
}
```

### Auth-Aware HTTP Instance

```typescript
// lib/http.ts
import axios from 'axios';
import { getAccessToken } from './auth';

export const http = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  timeout: 10_000,
});

http.interceptors.request.use(async config => {
  const token = await getAccessToken();
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

http.interceptors.response.use(
  res => res,
  err => {
    if (err.response?.status === 401) window.location.href = '/login';
    return Promise.reject(err);
  }
);
```


## 2.14 GitHub — Source Control and CI/CD

### Repository Structure

The platform uses a **polyrepo** layout: one GitHub repository per microservice plus a shared infrastructure repository. Each repo follows the same internal layout.

```
enterprise-org/
  orders-service/          ← GitHub repo per service
  identity-service/
  notifications-service/
  frontend/                ← React SPA repository
  platform-infra/          ← Shared IaC (CDK / Helm charts)
  .github/                 ← Org-level workflow templates
    workflow-templates/
      dotnet-service.yml
      react-app.yml
```

### Branch Model

```
main          ← production-ready; protected; no direct pushes
  └── release/1.x.x       ← optional stabilisation branch for hotfixes
  └── feature/orders-123  ← short-lived feature branch (< 2 days)
  └── fix/orders-456      ← short-lived bugfix branch
  └── chore/update-deps   ← dependency/tooling updates
```

**Rules enforced via GitHub branch protection on `main`:**
- Require pull request with ≥ 1 approved review before merging
- Dismiss stale reviews when new commits are pushed
- Require all status checks to pass (CI workflow jobs)
- Require branches to be up to date before merging
- Require signed commits (`git commit -S`)
- Restrict who can push (only GitHub Actions service account + repo admins)

### CODEOWNERS

```
# .github/CODEOWNERS
# Global fallback — all changes require a senior engineer review
*                          @enterprise-org/senior-engineers

# Domain and Application layer — domain team owns invariants
services/orders/src/Orders.Domain/          @enterprise-org/domain-team
services/orders/src/Orders.Application/     @enterprise-org/domain-team

# Infrastructure and CI — platform team owns
services/orders/src/Orders.Infrastructure/  @enterprise-org/platform-team
.github/                                    @enterprise-org/platform-team
platform-infra/                             @enterprise-org/platform-team

# Frontend
frontend/                                   @enterprise-org/frontend-team
```

### Pull Request Template

```markdown
<!-- .github/pull_request_template.md -->
## Summary
<!-- What does this PR do? Link to the issue/ticket. -->

## Type of change
- [ ] Bug fix
- [ ] New feature
- [ ] Refactoring (no functional change)
- [ ] Documentation
- [ ] Dependency update

## Checklist
- [ ] Tests added / updated
- [ ] Architecture tests still pass (`dotnet test --filter Category=Architecture`)
- [ ] No secrets or credentials committed
- [ ] Breaking changes documented in CHANGELOG.md
- [ ] PR diff < 400 lines (or justification provided)

## Screenshots / logs (if applicable)
```

### GitHub Environments and Secrets

```
Environments:
  staging     → auto-deploy on merge to main
  production  → requires manual approval from @platform-team

Repository secrets (set per-repo via Settings → Secrets):
  AWS_ECR_REGISTRY        ← ECR registry URL
  AWS_DEPLOY_ROLE         ← IAM role ARN for OIDC-based auth (no static keys)
  AZURE_CLIENT_ID         ← Azure service principal for ACR / Container Apps
  AZURE_TENANT_ID
  AZURE_SUBSCRIPTION_ID
  SONAR_TOKEN             ← SonarCloud analysis token
```

> **Security note:** All AWS authentication uses OIDC federation (`aws-actions/configure-aws-credentials` with `role-to-assume`). Static `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` secrets are never stored in GitHub.

### GitHub Actions — Full CI/CD Pipeline

The pipeline has four jobs that run in sequence: **lint-and-test → build-and-push → deploy-staging → deploy-production**.

```yaml
# .github/workflows/orders-service.yml
name: Orders Service CI/CD

on:
  push:
    branches: [main]
    paths: ['**']           # path filtering set at job level via dorny/paths-filter
  pull_request:
    branches: [main]

permissions:
  contents: read
  id-token: write           # required for OIDC-based AWS auth
  checks:  write            # required to publish test results

env:
  DOTNET_VERSION: '8.0.x'
  IMAGE_NAME:     orders-api
  SERVICE_PATH:   services/orders

jobs:
  # ── Job 1: Lint, build, and test ──────────────────────────────────────
  lint-and-test:
    name: Lint · Build · Test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }     # full history required by SonarCloud

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore
        working-directory: ${{ env.SERVICE_PATH }}

      - name: Build (Release)
        run: dotnet build --no-restore -c Release
        working-directory: ${{ env.SERVICE_PATH }}

      - name: Run all tests
        run: |
          dotnet test --no-build -c Release \
            --logger "trx;LogFileName=results.trx" \
            --collect:"XPlat Code Coverage" \
            --results-directory ./TestResults
        working-directory: ${{ env.SERVICE_PATH }}

      - name: Publish test results
        uses: dorny/test-reporter@v1
        if: always()
        with:
          name: xUnit Tests
          path: '**/TestResults/*.trx'
          reporter: dotnet-trx

      - name: Upload coverage to Codecov
        uses: codecov/codecov-action@v4
        with:
          files: '**/coverage.cobertura.xml'
          flags: orders-service

      - name: SonarCloud analysis
        uses: SonarSource/sonarcloud-github-action@master
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
          SONAR_TOKEN:  ${{ secrets.SONAR_TOKEN }}
        with:
          args: >
            -Dsonar.projectKey=enterprise-org_orders-service
            -Dsonar.organization=enterprise-org
            -Dsonar.cs.opencover.reportsPaths=**/coverage.opencover.xml

  # ── Job 2: Build Docker image and push to registry ────────────────────
  build-and-push:
    name: Build & Push Image
    needs: lint-and-test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    outputs:
      image-tag: ${{ steps.meta.outputs.version }}

    steps:
      - uses: actions/checkout@v4

      # AWS OIDC — no static credentials
      - uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-region:    us-east-1
          role-to-assume: ${{ secrets.AWS_DEPLOY_ROLE }}

      - uses: aws-actions/amazon-ecr-login@v2
        id: ecr-login

      - name: Extract Docker metadata
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ secrets.AWS_ECR_REGISTRY }}/${{ env.IMAGE_NAME }}
          tags: |
            type=sha,prefix=,format=short
            type=raw,value=latest,enable={{is_default_branch}}

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Build and push
        uses: docker/build-push-action@v5
        with:
          context:    ${{ env.SERVICE_PATH }}
          file:       ${{ env.SERVICE_PATH }}/src/Orders.Api/Dockerfile
          push:       true
          tags:       ${{ steps.meta.outputs.tags }}
          labels:     ${{ steps.meta.outputs.labels }}
          cache-from: type=gha
          cache-to:   type=gha,mode=max

      - name: Run Trivy vulnerability scan
        uses: aquasecurity/trivy-action@master
        with:
          image-ref: ${{ secrets.AWS_ECR_REGISTRY }}/${{ env.IMAGE_NAME }}:latest
          format:    sarif
          output:    trivy-results.sarif

      - name: Upload Trivy SARIF to GitHub Security
        uses: github/codeql-action/upload-sarif@v3
        with:
          sarif_file: trivy-results.sarif

  # ── Job 3: Deploy to Staging ──────────────────────────────────────────
  deploy-staging:
    name: Deploy → Staging
    needs: build-and-push
    runs-on: ubuntu-latest
    environment:
      name: staging
      url:  https://orders-api.staging.enterprise.example.com

    steps:
      - uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-region:    us-east-1
          role-to-assume: ${{ secrets.AWS_DEPLOY_ROLE }}

      - name: Deploy to ECS (staging)
        run: |
          aws ecs update-service \
            --cluster  enterprise-staging \
            --service  orders-api \
            --force-new-deployment

      - name: Wait for stable deployment
        run: |
          aws ecs wait services-stable \
            --cluster enterprise-staging \
            --services orders-api

      - name: Run smoke tests
        run: |
          curl --fail --retry 5 --retry-delay 5 \
            https://orders-api.staging.enterprise.example.com/health

  # ── Job 4: Deploy to Production (requires manual approval) ────────────
  deploy-production:
    name: Deploy → Production
    needs: deploy-staging
    runs-on: ubuntu-latest
    environment:
      name: production
      url:  https://orders-api.enterprise.example.com

    steps:
      - uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-region:    us-east-1
          role-to-assume: ${{ secrets.AWS_DEPLOY_ROLE }}

      - name: Deploy to ECS (production)
        run: |
          aws ecs update-service \
            --cluster  enterprise-production \
            --service  orders-api \
            --force-new-deployment

      - name: Wait for stable deployment
        run: |
          aws ecs wait services-stable \
            --cluster enterprise-production \
            --services orders-api

      - name: Tag release in GitHub
        uses: actions/github-script@v7
        with:
          script: |
            await github.rest.git.createRef({
              owner: context.repo.owner,
              repo:  context.repo.repo,
              ref:   `refs/tags/release-${context.sha.slice(0,7)}`,
              sha:   context.sha
            });
```

### GitHub Actions — Reusable Workflow for All .NET Services

Common test/build steps are extracted into a reusable workflow so every service opts in without duplicating YAML.

```yaml
# .github/workflows/reusable-dotnet-ci.yml
name: Reusable .NET CI

on:
  workflow_call:
    inputs:
      service-path:
        required: true
        type: string
      dotnet-version:
        required: false
        type: string
        default: '8.0.x'
    secrets:
      SONAR_TOKEN:
        required: true

jobs:
  ci:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ inputs.dotnet-version }}
      - run: dotnet restore
        working-directory: ${{ inputs.service-path }}
      - run: dotnet build --no-restore -c Release
        working-directory: ${{ inputs.service-path }}
      - run: dotnet test --no-build -c Release --logger trx --collect:"XPlat Code Coverage"
        working-directory: ${{ inputs.service-path }}
```

Calling workflow (minimal per-service file):

```yaml
# services/identity/.github/workflows/ci.yml
name: Identity Service CI
on:
  push:   { branches: [main] }
  pull_request: { branches: [main] }

jobs:
  ci:
    uses: enterprise-org/.github/.github/workflows/reusable-dotnet-ci.yml@main
    with:
      service-path: services/identity
    secrets:
      SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}
```

### GitHub Actions — Frontend Pipeline

```yaml
# .github/workflows/frontend.yml
name: Frontend CI/CD

on:
  push:
    branches: [main]
    paths: ['frontend/**']
  pull_request:
    paths: ['frontend/**']

jobs:
  test:
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: frontend
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: npm
          cache-dependency-path: frontend/package-lock.json

      - run: npm ci --frozen-lockfile
      - run: npm run lint
      - run: npm run type-check
      - run: npm run test -- --run --coverage

      - uses: codecov/codecov-action@v4
        with:
          flags: frontend

  build-and-deploy:
    needs: test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    defaults:
      run:
        working-directory: frontend
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: npm
          cache-dependency-path: frontend/package-lock.json

      - run: npm ci --frozen-lockfile
      - run: npm run build
        env:
          VITE_API_BASE_URL: ${{ vars.VITE_API_BASE_URL }}

      # Deploy static assets to S3 + CloudFront invalidation (AWS)
      - uses: aws-actions/configure-aws-credentials@v4
        with:
          aws-region:    us-east-1
          role-to-assume: ${{ secrets.AWS_DEPLOY_ROLE }}

      - name: Sync to S3
        run: |
          aws s3 sync dist/ s3://${{ vars.FRONTEND_BUCKET }} \
            --delete \
            --cache-control "public,max-age=31536000,immutable" \
            --exclude "index.html"
          aws s3 cp dist/index.html s3://${{ vars.FRONTEND_BUCKET }}/index.html \
            --cache-control "no-cache,no-store,must-revalidate"

      - name: Invalidate CloudFront
        run: |
          aws cloudfront create-invalidation \
            --distribution-id ${{ vars.CLOUDFRONT_DISTRIBUTION_ID }} \
            --paths "/*"
```

### Pipeline Flow Summary

```
Pull Request opened
        │
        ▼
  lint-and-test ──── fails ──► PR blocked (branch protection)
        │ passes
        ▼
  (merge to main)
        │
        ▼
  build-and-push ──► ECR image tagged with short SHA + Trivy scan
        │
        ▼
  deploy-staging ──► ECS Fargate (staging) + smoke test
        │
        ▼
  deploy-production ──► manual approval required
        │ approved
        ▼
  ECS Fargate (production) + GitHub release tag
```

## 2.15 Observability — Structured Logging and Tracing

```csharp
// Program.cs additions
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "orders-api")
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.OpenTelemetry());

// Activity tracing in a handler
public sealed class PlaceOrderHandler : IRequestHandler<PlaceOrderCommand, Guid>
{
    private static readonly ActivitySource ActivitySource = new("Orders.Application");

    public async Task<Guid> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        using var activity = ActivitySource.StartActivity("PlaceOrder");
        activity?.SetTag("customer.id", cmd.CustomerId);

        // ... handler logic
    }
}
```

**Log levels by concern:**

| Concern | Level |
|---|---|
| Request received / completed | Information |
| Domain exception (business rule violation) | Warning |
| Infrastructure exception (DB, HTTP) | Error |
| Unhandled exception | Critical |
| Debug traces | Debug (disabled in Production) |


## 2.16 Key Algorithms with Formal Specifications

### Algorithm: Order Total Recalculation

```pascal
ALGORITHM recalculateTotal(order)
INPUT:  order — Order aggregate with one or more OrderLine items
OUTPUT: total — Money value representing sum of all line totals

PRECONDITIONS:
  - order.Lines is non-empty
  - All OrderLine.UnitPrice values share the same Currency
  - All OrderLine.Quantity values are positive integers

POSTCONDITIONS:
  - total.Amount = Σ (line.UnitPrice.Amount × line.Quantity) for all lines in order.Lines
  - total.Currency = order.Lines[0].UnitPrice.Currency
  - total.Amount ≥ 0

LOOP INVARIANT:
  After processing k lines: accumulator.Amount = Σ (line[i].UnitPrice.Amount × line[i].Quantity) for i in [0, k)

BEGIN
  currency    ← order.Lines[0].UnitPrice.Currency
  accumulator ← Money(0, currency)

  FOR EACH line IN order.Lines DO
    ASSERT line.Quantity > 0
    ASSERT line.UnitPrice.Currency = currency
    lineTotal   ← Money(line.UnitPrice.Amount × line.Quantity, currency)
    accumulator ← accumulator + lineTotal
  END FOR

  ASSERT accumulator.Amount ≥ 0
  RETURN accumulator
END
```

### Algorithm: Discount Pipeline Application

```pascal
ALGORITHM applyDiscounts(basePrice, order, strategies)
INPUT:  basePrice  — Money (the undiscounted price)
        order      — Order aggregate
        strategies — ordered list of IDiscountStrategy implementations
OUTPUT: finalPrice — Money after all applicable discounts

PRECONDITIONS:
  - basePrice.Amount ≥ 0
  - strategies may be empty (identity case)

POSTCONDITIONS:
  - finalPrice.Amount ≤ basePrice.Amount  (discounts never increase price)
  - finalPrice.Amount ≥ 0
  - finalPrice.Currency = basePrice.Currency

BEGIN
  currentPrice ← basePrice

  FOR EACH strategy IN strategies DO
    reducedPrice ← strategy.Apply(currentPrice, order)
    ASSERT reducedPrice.Amount ≤ currentPrice.Amount
    ASSERT reducedPrice.Amount ≥ 0
    currentPrice ← reducedPrice
  END FOR

  RETURN currentPrice
END
```

### Algorithm: Outbox Message Processing

```pascal
ALGORITHM processOutboxBatch(batchSize, publisher, db)
INPUT:  batchSize — maximum messages to process per invocation
        publisher — message bus publisher
        db        — database context
OUTPUT: processedCount — number of messages successfully published

PRECONDITIONS:
  - batchSize > 0
  - publisher is connected
  - db connection is open

POSTCONDITIONS:
  - All fetched messages are either marked ProcessedAt = now() OR retain their original state
  - No message is published more than once per invocation (idempotency enforced by lock)

LOOP INVARIANT:
  Each message that enters the loop is either fully processed (published + marked) or fully rolled back

BEGIN
  messages ← db.OutboxMessages
                .WHERE ProcessedAt IS NULL
                .ORDER BY OccurredAt ASC
                .LIMIT batchSize
                .WITH PESSIMISTIC_LOCK

  processedCount ← 0

  FOR EACH message IN messages DO
    BEGIN TRANSACTION
      event ← deserialize(message.Payload, message.EventType)
      publisher.Publish(event)
      message.ProcessedAt ← now()
      db.Save(message)
      COMMIT
      processedCount ← processedCount + 1
    ON ERROR
      ROLLBACK
      log.Error("Failed to process outbox message", message.Id)
    END TRANSACTION
  END FOR

  RETURN processedCount
END
```


## 2.17 Correctness Properties

These properties encode invariants that must hold across all valid states and operations. They inform both domain logic implementation and property-based test generation.

### Domain Invariants

**P1 — Order line count:**
∀ order ∈ Orders : order.Lines.Count ≥ 1

**P2 — Order total consistency:**
∀ order ∈ Orders : order.Total.Amount = Σ (line.UnitPrice.Amount × line.Quantity) ∀ line ∈ order.Lines

**P3 — Non-negative total:**
∀ order ∈ Orders : order.Total.Amount ≥ 0

**P4 — Status transition validity:**
∀ order ∈ Orders : order.Status ∈ {Pending → Placed, Placed → Shipped, Placed → Cancelled, Pending → Cancelled}
No backward transitions are permitted.

**P5 — Money currency conservation:**
∀ a, b ∈ Money : a.Currency = b.Currency ⟹ (a + b).Currency = a.Currency

**P6 — Money addition commutativity:**
∀ a, b ∈ Money : a.Currency = b.Currency ⟹ (a + b).Amount = (b + a).Amount

**P7 — Discount non-inflation:**
∀ strategy ∈ DiscountStrategies, ∀ price ∈ Money : strategy.Apply(price, order).Amount ≤ price.Amount

### Application-Level Properties

**P8 — Handler idempotency (create):**
Placing the same order twice with identical IDs (idempotency key) must not produce two persisted orders.

**P9 — Domain event count:**
∀ command ∈ {PlaceOrder} : after successful handling, at least one domain event is published.

**P10 — Repository round-trip:**
∀ order saved via IOrderRepository.SaveAsync : IOrderRepository.GetByIdAsync(order.Id) returns an order with equal Id, Status, and Total.

### Clean Architecture Properties

**P11 — Dependency direction:**
∀ type T ∈ Domain : T has no compile-time dependency on Application, Infrastructure, or Api assemblies.

**P12 — Application isolation:**
∀ type T ∈ Application : T has no compile-time dependency on Infrastructure or Api assemblies.


## 2.18 LLM Cost and Context Management

The MCP Gateway routes tool calls to an LLM provider (OpenAI / AWS Bedrock). Unlike database queries — which have bounded, predictable costs — LLM API calls are priced by token consumption and can vary by orders of magnitude depending on context size, model selection, and call frequency. This section defines the strategy for controlling that cost while preserving response quality.

### 2.18.1 Token Consumption Concepts

```
┌─────────────────────────────────────────────────────────────────┐
│  LLM API Call                                                    │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Context Window  (model limit: e.g., 128K tokens)        │   │
│  │                                                           │   │
│  │  ┌─────────────┐  ┌──────────────┐  ┌────────────────┐  │   │
│  │  │System Prompt│  │ Conversation │  │ Tool Schemas + │  │   │
│  │  │ (fixed)     │  │ History      │  │ Results        │  │   │
│  │  │ ~500 tokens │  │ (variable)   │  │ (variable)     │  │   │
│  │  └─────────────┘  └──────────────┘  └────────────────┘  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                           ↓                                      │
│                    Completion tokens                             │
│                    (model response)                              │
└─────────────────────────────────────────────────────────────────┘

Cost = (input tokens + output tokens) × price per 1K tokens
```

**Key terms:**

| Term | Definition |
|---|---|
| Input tokens | Tokens consumed by the prompt: system prompt + history + tool schemas + tool results |
| Output tokens | Tokens in the model's response (typically 3–10× more expensive per token than input) |
| Context window | Maximum total tokens the model can process in a single call (e.g., 128K for GPT-4o) |
| Token budget | The maximum input tokens allocated to a single MCP tool call or conversation turn |
| Semantic cache | A cache that matches semantically similar prompts to avoid redundant LLM calls |

### 2.18.2 Model Selection Tiers

Not all tool calls need the most capable — or most expensive — model. Apply tiered model selection:

| Tier | Model examples | Cost profile | When to use |
|---|---|---|---|
| Tier 1 — Lightweight | GPT-4o-mini, Claude Haiku, Bedrock Titan | ~$0.15/1M input tokens | Simple lookups, structured data extraction, classification |
| Tier 2 — Standard | GPT-4o, Claude Sonnet | ~$2.50/1M input tokens | Multi-step reasoning, code generation, complex tool orchestration |
| Tier 3 — Heavy | o1, Claude Opus | ~$15+/1M input tokens | Architectural reasoning, long-context analysis; use sparingly |

**Implementation — model selector in the MCP Gateway:**

```csharp
// Orders.Api/Mcp/McpModelSelector.cs
public static class McpModelSelector
{
    // Tool complexity metadata drives model selection
    private static readonly Dictionary<string, McpModelTier> ToolTierMap = new()
    {
        ["get_order"]   = McpModelTier.Lightweight,   // simple lookup
        ["place_order"] = McpModelTier.Lightweight,   // structured write
        ["analyse_orders_trend"] = McpModelTier.Standard, // reasoning required
    };

    public static string SelectModel(string toolName) =>
        ToolTierMap.TryGetValue(toolName, out var tier) ? tier switch
        {
            McpModelTier.Lightweight => "gpt-4o-mini",
            McpModelTier.Standard    => "gpt-4o",
            McpModelTier.Heavy       => "o1",
            _                        => "gpt-4o-mini"
        } : "gpt-4o-mini"; // safe default
}

public enum McpModelTier { Lightweight, Standard, Heavy }
```

### 2.18.3 Context Window Budgeting

Control what enters the context window on each call to avoid runaway token spend.

```
Total context budget per tool call: 8,000 tokens (configurable per tier)

Allocation:
  System prompt          :   500 tokens (fixed; stored in config)
  Tool schema(s)         :   500 tokens (register only tools needed for this call)
  Conversation history   : 2,000 tokens (sliding window — drop oldest turns first)
  Tool result payload    : 4,000 tokens (truncate or summarise if result exceeds limit)
  Safety margin          : 1,000 tokens (completion headroom)
                         ---------
  Total                  : 8,000 tokens
```

**Implementation — context budget enforcer:**

```csharp
// Orders.Api/Mcp/ContextBudgetEnforcer.cs
public sealed class ContextBudgetEnforcer
{
    private const int MaxHistoryTokens  = 2_000;
    private const int MaxResultTokens   = 4_000;
    private const int ApproxCharsPerToken = 4; // conservative estimate

    // Truncate conversation history to fit within budget (drop oldest first)
    public IReadOnlyList<ChatMessage> TrimHistory(IList<ChatMessage> history)
    {
        var result = new List<ChatMessage>();
        int usedTokens = 0;

        foreach (var msg in history.Reverse())
        {
            int estimated = msg.Content.Length / ApproxCharsPerToken;
            if (usedTokens + estimated > MaxHistoryTokens) break;
            result.Insert(0, msg);
            usedTokens += estimated;
        }
        return result;
    }

    // Truncate tool result to fit within budget
    public string TrimResult(string resultJson)
    {
        int maxChars = MaxResultTokens * ApproxCharsPerToken;
        if (resultJson.Length <= maxChars) return resultJson;

        return resultJson[..maxChars] +
               "\n[...truncated: result exceeded context budget. Request a smaller page.]";
    }
}
```

### 2.18.4 Response Caching

Cache deterministic tool results to eliminate redundant LLM calls entirely. Applies when the same logical query produces the same answer within a TTL window.

```
Caching decision tree:

  Is the tool call deterministic (same input → same output)?
  ├── Yes: Is the result time-sensitive?
  │   ├── No  → Cache with long TTL (e.g., 1 hour)  — e.g., get_product_catalog
  │   └── Yes → Cache with short TTL (e.g., 30s)    — e.g., get_order (status may change)
  └── No  → Do not cache                             — e.g., analyse_trend (reasoning varies)
```

**Implementation — semantic + exact cache:**

```csharp
// Orders.Api/Mcp/McpResponseCache.cs
public sealed class McpResponseCache
{
    private readonly IDistributedCache _cache;

    public McpResponseCache(IDistributedCache cache) => _cache = cache;

    // Exact-match cache key: tool name + serialised arguments
    private static string CacheKey(string toolName, object args) =>
        $"mcp:{toolName}:{Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(args))))}";

    public async Task<string?> GetAsync(string toolName, object args, CancellationToken ct) =>
        await _cache.GetStringAsync(CacheKey(toolName, args), ct);

    public async Task SetAsync(
        string toolName, object args, string result,
        TimeSpan ttl, CancellationToken ct) =>
        await _cache.SetStringAsync(
            CacheKey(toolName, args), result,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
}
```

**Cache TTL guidelines by tool type:**

| Tool category | Example | Recommended TTL |
|---|---|---|
| Reference data lookup | get_product_catalog | 1 hour |
| Entity state lookup | get_order | 30 seconds |
| Aggregation / analytics | get_order_summary | 5 minutes |
| Write operation | place_order | No cache (always execute) |
| Reasoning / generation | analyse_trends | No cache |

### 2.18.5 Token Usage Observability

Log and trace token consumption on every LLM call so cost anomalies are detectable.

```csharp
// Orders.Api/Mcp/McpTelemetryMiddleware.cs
public sealed class McpTelemetryMiddleware
{
    private static readonly ActivitySource Source = new("MCP.Gateway");
    private static readonly Meter Meter = new("MCP.Gateway");

    private static readonly Counter<long> InputTokenCounter =
        Meter.CreateCounter<long>("mcp.tokens.input",  "tokens");
    private static readonly Counter<long> OutputTokenCounter =
        Meter.CreateCounter<long>("mcp.tokens.output", "tokens");
    private static readonly Counter<long> CacheHitCounter =
        Meter.CreateCounter<long>("mcp.cache.hits");

    public async Task<CallToolResult> InvokeAsync(
        string toolName, Func<Task<(CallToolResult result, Usage usage)>> handler)
    {
        using var activity = Source.StartActivity($"mcp.tool.{toolName}");

        var (result, usage) = await handler();

        // Tag dimensions for cost attribution
        var tags = new TagList
        {
            { "tool.name",  toolName },
            { "model.tier", McpModelSelector.SelectModel(toolName) }
        };

        InputTokenCounter.Add(usage.InputTokens,   tags);
        OutputTokenCounter.Add(usage.OutputTokens, tags);

        activity?.SetTag("mcp.tokens.input",  usage.InputTokens);
        activity?.SetTag("mcp.tokens.output", usage.OutputTokens);
        activity?.SetTag("mcp.cost.usd",      usage.EstimatedCostUsd);

        _logger.LogInformation(
            "MCP tool {ToolName} consumed {InputTokens} input + {OutputTokens} output tokens " +
            "(~${Cost:F4})",
            toolName, usage.InputTokens, usage.OutputTokens, usage.EstimatedCostUsd);

        return result;
    }
}
```

**OpenTelemetry metrics exported (surfaced in AWS X-Ray / Azure Monitor):**

| Metric name | Unit | Use |
|---|---|---|
| `mcp.tokens.input` | tokens | Track prompt cost by tool and model tier |
| `mcp.tokens.output` | tokens | Track completion cost (usually higher rate) |
| `mcp.cache.hits` | count | Measure cache effectiveness |
| `mcp.cost.usd` (span tag) | USD | Per-call cost attribution in traces |

### 2.18.6 Rate Limiting and Cost Guardrails

Prevent runaway spend from rogue clients or unexpected traffic spikes.

```csharp
// Program.cs additions
builder.Services.AddRateLimiter(options =>
{
    // Per-user token budget: max 50,000 tokens/hour
    options.AddFixedWindowLimiter("mcp-token-budget", cfg =>
    {
        cfg.Window           = TimeSpan.FromHours(1);
        cfg.PermitLimit      = 50;     // max 50 MCP tool calls per hour per user
        cfg.QueueLimit       = 5;      // queue up to 5 additional calls
        cfg.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

// Applied to the MCP route group
app.MapMcp("/mcp")
   .RequireRateLimiting("mcp-token-budget")
   .RequireAuthorization();
```

**Hard guardrail — monthly spend cap (infrastructure level):**

```
AWS Bedrock / OpenAI:
  - Set a hard monthly spend limit in the provider console
  - Configure a billing alert at 80% of the limit → SNS alert → PagerDuty
  - At 100%, disable the MCP Gateway endpoint until the limit resets

Alert thresholds (illustrative for a 100K DAU service):
  Monthly LLM budget : $500
  Alert at 80%       : $400 → warning notification
  Alert at 100%      : $500 → disable endpoint + incident ticket
```

### 2.18.7 Cost Estimation — Back-of-Envelope for MCP Gateway

Applying the §1.7 methodology to LLM API costs:

**Assumptions (100K DAU, Orders PoC)**

```
Users invoking MCP tools/day        : 1% of DAU    = 1,000 users/day
Tool calls per MCP session           : 3 calls
Total tool calls/day                 : 3,000 calls/day
Tool call distribution:
  get_order   (Tier 1, ~500 input tokens,  ~200 output) : 70% = 2,100 calls
  place_order (Tier 1, ~800 input tokens,  ~100 output) : 20% =   600 calls
  analyse_*   (Tier 2, ~2000 input tokens, ~500 output) : 10% =   300 calls
```

**Daily token consumption**

```
get_order   : 2,100 × (500 + 200)   =  1,470,000 tokens/day
place_order :   600 × (800 + 100)   =    540,000 tokens/day
analyse_*   :   300 × (2000 + 500)  =    750,000 tokens/day
                                    ─────────────────────────
Total                               =  2,760,000 tokens/day
Monthly                             = 82,800,000 tokens/month ≈ 83M tokens/month
```

**Monthly cost estimate**

```
Tier 1 tools (gpt-4o-mini @ $0.15 input / $0.60 output per 1M tokens):
  Input  : (1,470,000 + 540,000) × 30 = 60.3M input tokens  → $9.05
  Output : (1,470,000×200/700 + 540,000×100/900) × 30 ≈ 11M → $6.60

Tier 2 tools (gpt-4o @ $2.50 input / $10.00 output per 1M tokens):
  Input  : 300 × 2,000 × 30 = 18M input tokens              → $45.00
  Output : 300 × 500   × 30 =  4.5M output tokens           → $45.00
                                                        ────────────
Estimated monthly LLM cost (no cache)                       : ~$105/month

With 60% cache hit rate on get_order (30s TTL):
  Tier 1 calls reduced by 60%: saves ~$9.39
Estimated monthly LLM cost (with cache)                     : ~$96/month
```

**Cost scaling summary**

| DAU | Est. monthly LLM cost (no cache) | Est. monthly LLM cost (60% cache) |
|---|---|---|
| 10K | ~$11 | ~$10 |
| 100K | ~$105 | ~$96 |
| 1M | ~$1,050 | ~$960 |
| 10M | ~$10,500 | ~$9,600 |

> At 1M DAU the LLM cost (~$1K/month) is likely still small relative to infrastructure, but at 10M DAU it warrants aggressive caching, stricter tier enforcement, and possible fine-tuned model deployment.

---

# Part 3 — Conventions and Governance

## 3.1 Naming Conventions

| Artefact | Convention | Example |
|---|---|---|
| Aggregate | PascalCase noun | `Order`, `Customer` |
| Value Object | PascalCase noun | `Money`, `EmailAddress` |
| Domain Event | PastTense + Event suffix | `OrderPlacedEvent` |
| Command | Imperative + Command suffix | `PlaceOrderCommand` |
| Query | Get + noun + Query suffix | `GetOrderQuery` |
| Handler | Matches command/query + Handler | `PlaceOrderHandler` |
| Repository interface | I + noun + Repository | `IOrderRepository` |
| DTO | Noun + Dto | `OrderDto` |
| Endpoint class | Plural noun + Endpoints | `OrdersEndpoints` |
| Test class | Subject + Tests | `OrderTests`, `PlaceOrderHandlerTests` |

## 3.2 Branching and Commit Strategy

- **Trunk-based development** — short-lived feature branches (≤ 2 days) merged to `main` via PR; no long-running release branches except for hotfixes
- **Branch naming** — `feature/<ticket-id>-short-description`, `fix/<ticket-id>-short-description`, `chore/update-deps`
- **Commit messages** — [Conventional Commits](https://www.conventionalcommits.org/): `feat(orders): add cancellation endpoint`, `fix(money): handle zero-amount edge case`
- **PR size** — target < 400 line diff; split larger changes into stacked PRs linked in sequence
- **Required status checks** — all GitHub Actions CI jobs must be green before merge is allowed
- **Merge strategy** — Squash merge for feature branches (clean linear history on `main`); merge commit for release branches
- **Auto-delete branches** — enabled on the GitHub repository so merged branches are removed automatically
- **Tagging** — production deployments are tagged automatically by the GitHub Actions pipeline (`release-<sha>`)
- **Semantic versioning** — services follow SemVer; the `release-please` GitHub Action automates CHANGELOG.md generation and version bumps from Conventional Commits

## 3.3 Security Checklist

- All endpoints require `RequireAuthorization()` unless explicitly opted out with `AllowAnonymous()`
- Secrets stored in AWS Secrets Manager or Azure Key Vault, injected at runtime — never in source
- All user-supplied inputs validated by FluentValidation before reaching the domain layer
- Database connections use parameterised queries only (EF Core enforces this by default)
- Docker images run as non-root user; images rebuilt weekly for base OS patches
- OWASP Top 10 reviewed per service during security review before first production deployment

## 3.4 Performance Guidelines

- Avoid N+1 queries — use `Include` / `ThenInclude` or separate batch queries
- Use `AsNoTracking()` for read-only queries that do not update the entity
- Cache read-heavy aggregates with the `CachedOrderRepository` decorator pattern
- All repository and HTTP calls accept and propagate `CancellationToken`
- Limit message payload size to < 64 KB; use claim-check pattern for larger payloads

---

# Appendix A — Dependency Reference

| Package | Version | Purpose |
|---|---|---|
| MediatR | 12.x | CQRS mediator |
| FluentValidation | 11.x | Input validation |
| MassTransit | 8.x | Message bus abstraction |
| Entity Framework Core | 8.x | ORM |
| Npgsql.EFCore | 8.x | PostgreSQL EF provider |
| Serilog | 3.x | Structured logging |
| OpenTelemetry .NET | 1.x | Traces and metrics |
| Polly (via HttpResilience) | 8.x | Retry, circuit breaker |
| FluentAssertions | 6.x | Test assertions |
| xUnit | 2.x | Test framework |
| Moq | 4.x | Mocking |
| Bogus | 35.x | Fake data generation |
| FsCheck.Xunit | 2.x | Property-based testing |
| NetArchTest.Rules | 1.x | Architecture enforcement |
| ModelContextProtocol | 0.x | MCP SDK for .NET |

---

# Appendix B — Glossary

| Term | Definition |
|---|---|
| Aggregate | A cluster of domain objects treated as a single unit with one root entity |
| Aggregate Root | The single entry point for all operations on an aggregate |
| Bounded Context | An explicit boundary within which a domain model applies consistently |
| Capacity Estimation | Rough quantitative reasoning about QPS, storage, bandwidth, and latency to validate architectural choices before implementation |
| Clean Architecture | A layered architectural style where dependencies point inward toward the domain |
| CQRS | Command Query Responsibility Segregation — separate read and write models |
| Decision Gate | A threshold derived from back-of-the-envelope estimation that triggers a specific architectural choice (e.g., adding read replicas when peak QPS > 1,000) |
| Domain Event | An immutable record of something that happened in the domain |
| Entity | An object with a distinct identity that persists over time |
| MCP | Model Context Protocol — a protocol for exposing tools to AI/LLM agents |
| Outbox Pattern | Reliable event publishing by persisting events in the same transaction as the domain change |
| Repository | An abstraction over data persistence for a specific aggregate |
| Specification | An encapsulated business rule that can be combined and reused |
| Value Object | An immutable object defined entirely by its attributes, with no identity |
