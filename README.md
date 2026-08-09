# Enterprise App Architecture (EAA)

A reference implementation of a production-grade enterprise platform built with Clean Architecture, Domain-Driven Design, and CQRS/Event-Driven patterns.

## Overview

This repository provides a .NET 8+ backend project structure following enterprise best practices. It ships as a `dotnet new` template so teams can scaffold new services quickly.

## Tech Stack

### Backend (.NET 8+)

- **Architecture**: Clean Architecture (Domain → Application → Infrastructure → API)
- **CQRS**: MediatR for command/query separation with pipeline behaviours
- **Messaging**: MassTransit + RabbitMQ with transactional outbox pattern
- **Persistence**: Entity Framework Core + PostgreSQL
- **Observability**: OpenTelemetry (tracing, metrics) + Serilog (structured logging)
- **Validation**: FluentValidation
- **Auth**: JWT Bearer authentication
- **API**: ASP.NET Core Minimal APIs

### Frontend (React + TypeScript)

- **Build**: Vite 6
- **Server State**: TanStack Query 5
- **Client State**: Zustand 5
- **HTTP**: Axios
- **Testing**: Vitest + @testing-library/react + fast-check

### Infrastructure

- **Database**: PostgreSQL 16
- **Message Broker**: RabbitMQ 3.13
- **Containerization**: Docker + Docker Compose
- **CI/CD**: GitHub Actions
- **Cloud**: AWS (ECS Fargate, RDS Aurora, SNS/SQS) or Azure (Container Apps, Azure SQL, Service Bus)

## Project Structure

```
├── src/
│   ├── {SolutionName}.Domain/           # Aggregates, entities, value objects, domain events
│   ├── {SolutionName}.Application/      # Commands, queries, handlers, DTOs, behaviours
│   ├── {SolutionName}.Infrastructure/   # EF Core, MassTransit, HTTP clients, caching
│   └── {SolutionName}.Api/              # Minimal API endpoints, middleware
├── tests/
│   ├── {SolutionName}.Domain.Tests/     # Domain unit tests + property tests
│   ├── {SolutionName}.Application.Tests/# Handler tests (mocked deps)
│   ├── {SolutionName}.Infrastructure.Tests/ # Outbox, messaging, persistence tests
│   ├── {SolutionName}.Api.Tests/        # Middleware & endpoint property tests
│   ├── {SolutionName}.Architecture.Tests/  # NetArchTest dependency rule enforcement
│   └── {SolutionName}.Integration.Tests/   # End-to-end tests (Testcontainers)
├── frontend/                            # React SPA (Vite + TanStack Query + Zustand)
├── docs/                                # ADRs, cloud topology, sizing, security
└── .kiro/steering/                      # AI agent steering files (conventions & guides)
```

## Getting Started

### Prerequisites

- [.NET 8+ SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Node.js 20+](https://nodejs.org/) (for frontend)

### Run Locally

```bash
# Backend
dotnet build
dotnet run --project src/{SolutionName}.Api

# Frontend
cd frontend
npm install
npm run dev

# Full stack (Docker Compose)
docker-compose up
```

### Run Tests

```bash
# Backend
dotnet test

# Frontend
cd frontend
npm test
```

## API Operational Endpoints

| Endpoint | Purpose | Auth Required |
|----------|---------|---------------|
| `GET /health/live` | Liveness probe (always 200 if process is running) | No |
| `GET /health/ready` | Readiness probe (checks PostgreSQL + RabbitMQ) | No |
| `GET /openapi/v1.json` | OpenAPI 3.0 specification | No |
| `GET /swagger` | Swagger UI (Development only) | No |

## Security

- **Security headers**: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Server` header removed, HSTS on HTTPS connections.
- **Rate limiting**: Fixed-window per user/IP with configurable limits.
- **Request size**: Bodies exceeding 1 MB are rejected with HTTP 413 before deserialization.
- **Correlation IDs**: `X-Correlation-Id` header propagated across HTTP requests, outbox, and message consumers.

---

## Architecture Decision Records (ADRs)

ADRs document significant architectural choices and their rationale. Located in `docs/adr/`.

| ADR | Title | Summary |
|-----|-------|---------|
| [ADR-001](docs/adr/ADR-001-clean-architecture.md) | Clean Architecture | Four-layer structure with strict dependency rule enforced by NetArchTest |
| [ADR-002](docs/adr/ADR-002-mediatr-cqrs.md) | MediatR for CQRS | In-process mediator with pipeline behaviours for validation and logging |
| [ADR-003](docs/adr/ADR-003-masstransit-messaging.md) | MassTransit Messaging | Transport abstraction over RabbitMQ/SNS/Service Bus with consumer retry and DLQ |
| [ADR-004](docs/adr/ADR-004-outbox-pattern.md) | Outbox Pattern | Transactional outbox for guaranteed at-least-once event delivery |
| [ADR-005](docs/adr/ADR-005-efcore-orm.md) | EF Core ORM | Rich domain mapping with strongly-typed IDs, owned entities, and parameterized queries |
| [ADR-006](docs/adr/ADR-006-rate-limiting-security-headers.md) | Rate Limiting & Security Headers | Fixed-window rate limiter and OWASP security headers middleware |
| [ADR-007](docs/adr/ADR-007-observability-correlation.md) | Observability & Correlation | OpenTelemetry metrics/traces, local observability stack, correlation ID propagation |
| [ADR-008](docs/adr/ADR-008-microservices-architecture.md) | Microservices Architecture | Bounded context ownership, async-first communication, independent deployability |
| [ADR-009](docs/adr/ADR-009-testing-framework-selection.md) | Testing Framework Selection | xUnit + FsCheck + PBT strategy, Vitest + fast-check for frontend |
| [ADR-010](docs/adr/ADR-010-frontend-technology-selection.md) | Frontend Technology Selection | React + Vite + TanStack Query + Zustand, feature-module architecture |
| [ADR-011](docs/adr/ADR-011-manual-object-mapping.md) | Manual Object Mapping | Static `From()` methods over AutoMapper/Mapster for debuggability and compile-time safety |

---

## Steering Files Overview

Steering files provide AI-agent conventions and development guidelines. Located in `.kiro/steering/`. They supply context to Kiro so it follows the project's patterns and conventions.

### Inclusion Modes

Each steering file declares an `inclusion` mode in its YAML frontmatter that controls **when** it gets loaded into context:

| Mode | Frontmatter | Behavior | Token Impact |
|------|-------------|----------|--------------|
| **auto** | `inclusion: auto` | Loaded on every interaction | Always consumed |
| **fileMatch** | `inclusion: fileMatch` + `fileMatchPattern: "glob"` | Loaded only when a matching file is open | Conditional |
| **manual** | `inclusion: manual` | Loaded only when explicitly referenced with `#` in chat | On-demand |

**Example frontmatter:**

```yaml
---
inclusion: fileMatch
fileMatchPattern: "**/*Command*.cs,**/*Query*.cs,**/*Handler*.cs"
---
```

### Why This Matters

Loading all steering files on every interaction wastes context tokens. This project uses a tiered strategy:

- **auto** — Core conventions that apply to almost every task (architecture layers, DDD patterns, commit standards).
- **fileMatch** — Domain-specific guides loaded when you're working on relevant code (EF Core rules appear only when editing repositories, React rules only in `frontend/`).
- **manual** — Reference material for occasional deep-dives (design patterns, scaling strategies, SOLID principles). Reference them with `#12-solid-principles` in chat when needed.

### How to Use Manual Steering Files

In Kiro's chat input, type `#` followed by the steering file name to include it:

```
#25-caching-best-practices
```

This loads the file into context for that interaction only, keeping your baseline token budget lean.

### Current Configuration

| Inclusion | Files |
|-----------|-------|
| **auto** | `01` (Clean Architecture), `02` (DDD), `08` (Commits & PRs) |
| **fileMatch** | `03` (CQRS), `04` (MassTransit), `05` (Minimal API), `06` (EF Core Config), `07` (Testing), `09` (React), `10` (Docker/CI), `11` (Middleware), `16` (EF Core), `17` (Event-Driven), `20` (Logging), `21` (Configuration), `24` (Mapping) |
| **manual** | `12` (SOLID), `13` (Design Patterns), `14` (Code Review), `15` (Microservices), `18` (Arch Principles), `19` (Testing Strategy), `22` (REST API), `23` (Code Smells), `25` (Caching), `26` (Async), `27` (Security), `28` (Scaling), `29` (iSAQB) |

---

### Full Steering File Reference

| # | File | Purpose |
|---|------|---------|
| 01 | `01-clean-architecture-layer-placement.md` | Layer responsibilities, dependency rules, decision checklist |
| 02 | `02-ddd-aggregate-entity-creation.md` | Strongly-typed IDs, entity base classes, aggregate root patterns |
| 03 | `03-cqrs-command-query-scaffolding.md` | Command/query definitions, validators, handlers, DTOs, pipeline behaviours |
| 04 | `04-masstransit-consumer-event-publishing.md` | Domain events, outbox flow, consumer creation, correlation ID propagation |
| 05 | `05-minimal-api-endpoint-conventions.md` | Endpoint groups, route patterns, status codes, rate limiting, MediatR dispatch |
| 06 | `06-efcore-entity-configuration.md` | Entity type configurations, table naming, ID conversions, owned entities |
| 07 | `07-testing-conventions.md` | Test structure, naming, domain/handler/architecture/integration test patterns, PBT |
| 08 | `08-conventional-commits-pr-standards.md` | Commit format, PR template, breaking changes, diff size limits |
| 09 | `09-react-feature-module.md` | Feature directory structure, TanStack Query hooks, Zustand stores, error handling |
| 10 | `10-docker-cicd-awareness.md` | Docker Compose services, CI/CD pipeline stages, environment variables |
| 11 | `11-middleware-security-observability.md` | Middleware pipeline order, security headers, rate limiting, OpenTelemetry, CORS |
| 12 | `12-solid-principles.md` | SRP, OCP, LSP, ISP, DIP applied to each layer with examples and anti-patterns |
| 13 | `13-design-patterns.md` | Factory, Repository, Decorator, Mediator, Observer, Strategy, CQRS, Outbox |
| 14 | `14-code-review-practices.md` | Review turnaround, comment categories, .NET/React/testing/security checklists |
| 15 | `15-microservices-best-practices.md` | Service boundaries, communication patterns, resilience, data ownership, observability |
| 16 | `16-efcore-best-practices.md` | Query performance, change tracker, concurrency, migrations, connection pooling, anti-patterns |
| 17 | `17-event-driven-messaging.md` | Event design, schema versioning, idempotency, ordering, DLQ handling, saga patterns |
| 18 | `18-architectural-principles.md` | Separation of Concerns, DRY, KISS, YAGNI with examples and conflict resolution |
| 19 | `19-testing-strategy.md` | Testing pyramid, test doubles taxonomy, isolation rules, PBT strategy, frontend testing, contracts |
| 20 | `20-logging-patterns.md` | Log levels, structured templates, correlation, exception logging, what to log/avoid, configuration |
| 21 | `21-configuration-options-pattern.md` | Options pattern, validation, environment overrides, secrets management, feature flags |
| 22 | `22-restful-api-best-practices.md` | Resource naming, HTTP semantics, ProblemDetails, pagination, versioning, caching, integrations |
| 23 | `23-code-smells-antipatterns.md` | God class, feature envy, primitive obsession, anemic model, prop drilling, detection checklist |
| 24 | `24-object-mapping-conventions.md` | Manual mapping strategy, From() pattern, direction rules, null handling, anti-patterns |
| 25 | `25-caching-best-practices.md` | IMemoryCache vs. Redis, cache-aside pattern, invalidation, stampede prevention, key design |
| 26 | `26-async-patterns.md` | Async all the way, CancellationToken, Task.WhenAll, background services, pitfalls |
| 27 | `27-security-auth-patterns.md` | JWT auth, policy-based authorization, secure coding, input validation, CORS, encryption |
| 28 | `28-scaling-system-design.md` | Progressive scaling stages (0→millions), auto-scaling, sharding, performance budgets |
| 29 | `29-architecture-fundamentals-isaqb.md` | Quality attributes (ISO 25010), C4 documentation, coupling/cohesion, tech debt, ATAM |

---

## Use as a Template (NuGet Package)

This repository is published as a `dotnet new` template on GitHub Packages.

### Package Info

| Field | Value |
|-------|-------|
| Package | `Eolivas.EnterpriseAppArchitecture` |
| Feed URL | `https://nuget.pkg.github.com/eolivas/index.json` |
| Short name | `eaa-solution` |

### Setup (one-time)

1. Create a [GitHub Personal Access Token](https://github.com/settings/tokens) (**classic**, not fine-grained) with `read:packages` scope.

2. Add the GitHub Packages source to your NuGet config:

```bash
dotnet nuget add source "https://nuget.pkg.github.com/eolivas/index.json" \
  --name github-eolivas \
  --username eolivas \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text
```

3. Install the template:

```bash
dotnet new install Eolivas.EnterpriseAppArchitecture
```

### Create a new project

```bash
mkdir MyNewService
cd MyNewService
dotnet new eaa-solution -n MyNewService
```

The `-n` parameter sets your project name, which gets applied across the solution file, project files, and namespaces (e.g., `MyNewService.Domain`, `MyNewService.Application`, etc.).

### Update to the latest template version

```bash
dotnet new install Eolivas.EnterpriseAppArchitecture
```

### Uninstall

```bash
dotnet new uninstall Eolivas.EnterpriseAppArchitecture
```

## Publishing a New Template Version

Maintainers publish new versions by tagging a commit:

```bash
git tag v1.x.x
git push origin v1.x.x
```

The `publish-template.yml` GitHub Action automatically packs and pushes the NuGet package to GitHub Packages.

See [CHANGELOG.md](CHANGELOG.md) for version history.

## License

Private repository. All rights reserved.
