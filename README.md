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
│   ├── Orders.Infrastructure.Tests/
│   ├── Orders.Api.Tests/
│   └── Orders.Architecture.Tests/  # NetArchTest dependency rule enforcement
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

## Architecture Decisions

Key decisions are documented as ADRs in `docs/adr/`:

| ADR | Decision |
|-----|----------|
| [001](docs/adr/ADR-001-clean-architecture.md) | Clean Architecture as structural foundation |
| [002](docs/adr/ADR-002-mediatr-cqrs.md) | MediatR for CQRS |
| [003](docs/adr/ADR-003-masstransit-messaging.md) | MassTransit for async messaging |
| [004](docs/adr/ADR-004-outbox-pattern.md) | Transactional outbox for reliable event publishing |
| [005](docs/adr/ADR-005-efcore-orm.md) | EF Core as ORM |

## Bounded Contexts

The platform is decomposed into three bounded contexts:

- **Orders** (core domain) — Order lifecycle management with aggregate root, domain events, and strict status transitions
- **Identity** (upstream) — User registration and authentication
- **Notifications** (downstream) — Event-driven email/SMS/push notifications

See [docs/bounded-contexts.md](docs/bounded-contexts.md) for the full context map.

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
