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

## License

Private repository. All rights reserved.
