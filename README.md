# Enterprise App Architecture (EAA)

A reference implementation of a production-grade enterprise platform built with Clean Architecture, Domain-Driven Design, and CQRS/Event-Driven patterns.

## Overview

This repository demonstrates how to structure a .NET 8 backend with a React frontend following enterprise best practices. The primary bounded context implemented is the **Orders Service**, with event-driven integration points for Identity and Notifications services.

## Tech Stack

### Backend (.NET 8)

- **Architecture**: Clean Architecture (Domain → Application → Infrastructure → API)
- **CQRS**: MediatR for command/query separation with pipeline behaviours
- **Messaging**: MassTransit + RabbitMQ with transactional outbox pattern
- **Persistence**: Entity Framework Core + PostgreSQL
- **Observability**: OpenTelemetry (tracing, metrics) + Serilog (structured logging)
- **Validation**: FluentValidation
- **Auth**: JWT Bearer authentication
- **API**: ASP.NET Core Minimal APIs + MCP (Model Context Protocol) tooling

### Frontend (React 18)

- **Build**: Vite + TypeScript
- **State**: Zustand + TanStack React Query
- **HTTP**: Axios
- **Testing**: Vitest + Testing Library

### Infrastructure

- **Database**: PostgreSQL 16
- **Message Broker**: RabbitMQ 3.13
- **Containerization**: Docker + Docker Compose
- **CI/CD**: GitHub Actions

## Project Structure

```
├── src/
│   ├── Orders.Domain/           # Aggregates, entities, value objects, domain events
│   ├── Orders.Application/      # Commands, queries, handlers, DTOs, behaviours
│   ├── Orders.Infrastructure/   # EF Core, MassTransit, HTTP clients, caching
│   └── Orders.Api/              # Minimal API endpoints, middleware, MCP tools
├── tests/
│   ├── Orders.Domain.Tests/     # Domain unit & property-based tests
│   ├── Orders.Application.Tests/# Handler tests
│   ├── Orders.Infrastructure.Tests/ # Outbox, messaging, persistence tests
│   ├── Orders.Api.Tests/        # Middleware & endpoint property tests
│   ├── Orders.Architecture.Tests/  # NetArchTest dependency rule enforcement
│   └── Orders.Integration.Tests/   # WebApplicationFactory + Testcontainers end-to-end tests
├── frontend/                    # React SPA
├── docs/
│   ├── adr/                     # Architecture Decision Records
│   ├── cloud-topology/          # AWS & Azure deployment topologies
│   ├── sizing/                  # Capacity estimation
│   └── llm-cost/                # LLM cost estimation
└── docker-compose.yml
```

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Run with Docker Compose

```bash
docker compose up --build
```

This starts:
- **Orders API** at `http://localhost:5000`
- **Frontend** at `http://localhost:3000`
- **PostgreSQL** at `localhost:5432`
- **RabbitMQ Management** at `http://localhost:15672` (guest/guest)
- **Jaeger UI** (traces) at `http://localhost:16686`
- **Prometheus** (metrics) at `http://localhost:9090`

### Run Locally (without Docker)

```bash
# Backend
dotnet build
dotnet run --project src/Orders.Api

# Frontend
cd frontend
npm install
npm run dev
```

### Run Tests

```bash
# All .NET tests
dotnet test

# Frontend tests
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

## Security & Rate Limiting

- **Rate limiting**: Fixed-window rate limit (default 100 req/min) on `/api/orders`, partitioned by authenticated user or client IP. Returns `429` with `Retry-After` header on excess. Configurable via `RateLimit:PermitLimit` and `RateLimit:WindowSeconds`.
- **Security headers**: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Server` header removed, HSTS on HTTPS connections.
- **CORS**: Configurable allowed origins via `Cors:AllowedOrigins`. Credentials allowed, preflight cached for 600s.
- **Request size**: Bodies exceeding 1 MB are rejected with HTTP 413 before deserialization.
- **Correlation IDs**: `X-Correlation-Id` header is propagated across HTTP requests, log entries, and MassTransit messages for distributed tracing.

## Architecture Decisions

Key decisions are documented as ADRs in `docs/adr/`:

| ADR | Decision |
|-----|----------|
| [001](docs/adr/ADR-001-clean-architecture.md) | Clean Architecture as structural foundation |
| [002](docs/adr/ADR-002-mediatr-cqrs.md) | MediatR for CQRS |
| [003](docs/adr/ADR-003-masstransit-messaging.md) | MassTransit for async messaging |
| [004](docs/adr/ADR-004-outbox-pattern.md) | Transactional outbox for reliable event publishing |
| [005](docs/adr/ADR-005-efcore-orm.md) | EF Core as ORM |
| [006](docs/adr/ADR-006-rate-limiting-security-headers.md) | API rate limiting and security headers |
| [007](docs/adr/ADR-007-observability-correlation.md) | Observability stack and correlation ID propagation |

## Bounded Contexts

The platform is decomposed into three bounded contexts:

- **Orders** (core domain) — Order lifecycle management with aggregate root, domain events, and strict status transitions
- **Identity** (upstream) — User registration and authentication
- **Notifications** (downstream) — Event-driven email/SMS/push notifications

See [docs/bounded-contexts.md](docs/bounded-contexts.md) for the full context map.

## Kiro Steering Skills

This project includes 11 steering files in `.kiro/steering/` that guide AI-assisted development to follow the project's established patterns. They are automatically loaded into context.

| # | Skill | What It Guides |
|---|-------|----------------|
| 01 | [Clean Architecture Layer Placement](`.kiro/steering/01-clean-architecture-layer-placement.md`) | Where new code goes (Domain vs Application vs Infrastructure vs Api) and the dependency rule |
| 02 | [DDD Aggregate & Entity Creation](`.kiro/steering/02-ddd-aggregate-entity-creation.md`) | Static factory methods, private setters, strongly-typed IDs, domain events, invariant enforcement |
| 03 | [CQRS Command/Query Scaffolding](`.kiro/steering/03-cqrs-command-query-scaffolding.md`) | MediatR commands, queries, handlers, FluentValidation, pipeline behaviours, DTOs |
| 04 | [MassTransit Consumer & Event Publishing](`.kiro/steering/04-masstransit-consumer-event-publishing.md`) | Domain events, outbox pattern (retry/dead-letter/retention), consumer creation, correlation ID propagation |
| 05 | [Minimal API Endpoint Conventions](`.kiro/steering/05-minimal-api-endpoint-conventions.md`) | Route groups, authorization, rate limiting, ISender dispatch, HTTP status codes, health checks |
| 06 | [EF Core Entity Configuration](`.kiro/steering/06-efcore-entity-configuration.md`) | Value conversions, owned entities, PropertyAccessMode.Field, snake_case table naming |
| 07 | [Testing Conventions](`.kiro/steering/07-testing-conventions.md`) | xUnit, FsCheck property tests, fast-check (frontend), integration tests (Testcontainers), architecture tests |
| 08 | [Conventional Commits & PR Standards](`.kiro/steering/08-conventional-commits-pr-standards.md`) | Commit format, type/scope, breaking changes, PR template, 400-line diff limit |
| 09 | [React Feature Module](`.kiro/steering/09-react-feature-module.md`) | Feature folder structure, TanStack Query hooks, Zustand stores, error handling, ErrorBoundary |
| 10 | [Docker & CI/CD Awareness](`.kiro/steering/10-docker-cicd-awareness.md`) | docker-compose services (including observability stack), CI pipeline stages, EF migrations validation |
| 11 | [Middleware, Security & Observability](`.kiro/steering/11-middleware-security-observability.md`) | Middleware pipeline order, security headers, correlation IDs, rate limiting, CORS, health checks, OpenTelemetry |

## Commit Conventions

This project follows [Conventional Commits](https://www.conventionalcommits.org/). See [docs/REPO_CONVENTIONS.md](docs/REPO_CONVENTIONS.md) for details.

## Use as a Template (NuGet Package)

This repository is published as a `dotnet new` template on GitHub Packages. Teams can scaffold new projects from this architecture baseline.

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

The `-n` parameter sets your project name. The template uses `Orders` as a placeholder — it gets replaced with your chosen name across the solution file, project files, and namespaces (e.g., `MyNewService.Domain`, `MyNewService.Application`, etc.).

### Update to the latest template version

```bash
dotnet new install Eolivas.EnterpriseAppArchitecture
```

Re-running the install command pulls the latest published version. Existing projects are not affected — only new scaffolding uses the updated template.

### Uninstall

```bash
dotnet new uninstall Eolivas.EnterpriseAppArchitecture
```

## Publishing a New Template Version

Maintainers publish new versions by tagging a commit:

```bash
git tag v1.1.0
git push origin v1.1.0
```

The `publish-template.yml` GitHub Action automatically packs and pushes the NuGet package to GitHub Packages.

See [CHANGELOG.md](CHANGELOG.md) for version history.

## License

Private repository. All rights reserved.
