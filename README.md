# Enterprise App Architecture (EAA)

A reference implementation of a production-grade enterprise platform built with Clean Architecture, Domain-Driven Design, and CQRS/Event-Driven patterns.

## Overview

This repository provides a .NET 8 backend project structure following enterprise best practices. It ships as a `dotnet new` template so teams can scaffold new services quickly.

## Tech Stack

### Backend (.NET 8)

- **Architecture**: Clean Architecture (Domain → Application → Infrastructure → API)
- **CQRS**: MediatR for command/query separation with pipeline behaviours
- **Messaging**: MassTransit + RabbitMQ with transactional outbox pattern
- **Persistence**: Entity Framework Core + PostgreSQL
- **Observability**: OpenTelemetry (tracing, metrics) + Serilog (structured logging)
- **Validation**: FluentValidation
- **Auth**: JWT Bearer authentication
- **API**: ASP.NET Core Minimal APIs

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
│   └── Orders.Api/              # Minimal API endpoints, middleware
├── tests/
│   ├── Orders.Domain.Tests/     # Domain unit tests
│   ├── Orders.Application.Tests/# Handler tests
│   ├── Orders.Infrastructure.Tests/ # Infrastructure tests
│   ├── Orders.Api.Tests/        # API tests
│   ├── Orders.Architecture.Tests/  # NetArchTest dependency rule enforcement
│   └── Orders.Integration.Tests/   # Integration tests
└── docs/
```

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Run Locally

```bash
dotnet build
dotnet run --project src/Orders.Api
```

### Run Tests

```bash
dotnet test
```

## API Operational Endpoints

| Endpoint | Purpose | Auth Required |
|----------|---------|---------------|
| `GET /health/live` | Liveness probe (always 200 if process is running) | No |
| `GET /health/ready` | Readiness probe | No |
| `GET /openapi/v1.json` | OpenAPI 3.0 specification | No |
| `GET /swagger` | Swagger UI (Development only) | No |

## Security

- **Security headers**: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Server` header removed, HSTS on HTTPS connections.
- **Request size**: Bodies exceeding 1 MB are rejected with HTTP 413 before deserialization.
- **Correlation IDs**: `X-Correlation-Id` header is propagated across HTTP requests and log entries for distributed tracing.

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
