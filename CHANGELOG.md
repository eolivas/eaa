# Changelog

All notable changes to this template will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- MassTransit RabbitMQ transport with conditional fallback to InMemory (graceful degradation when broker unavailable)
- Outbox pattern hardening: batch processing, configurable retry counting, dead-lettering (FailedAt/FailureReason), and OTEL metrics (processed/failed counters, duration histogram)
- Outbox retention background service with configurable schedule and batch deletion
- EF Core migrations infrastructure: initial migration, design-time factory, auto-migrate in Development, pending-migration warning in Production
- Database seeding for Development environment (orders in each lifecycle state)
- Health check endpoints: `GET /health/live` (liveness) and `GET /health/ready` (readiness with PostgreSQL + RabbitMQ checks)
- OpenTelemetry metrics export (`WithMetrics`) alongside existing tracing, with ASP.NET Core and custom meter instrumentation
- Docker Compose observability stack: OTEL Collector, Jaeger (traces on port 16686), Prometheus (metrics on port 9090)
- Correlation ID middleware: extract/generate `X-Correlation-Id`, propagate through Serilog LogContext and MassTransit message headers
- CORS configuration via `Cors:AllowedOrigins` with credentials, preflight caching, and allowed headers/methods
- Fixed-window rate limiting on `/api/orders` with `X-RateLimit-Limit`, `X-RateLimit-Remaining`, and `Retry-After` headers
- OpenAPI 3.0 specification at `/openapi/v1.json`; Swagger UI at `/swagger` (Development only)
- HTTP client resilience pipeline registration (retry, circuit breaker, timeout) for external service calls
- Security headers middleware: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Server` header removal, conditional HSTS
- Input validation: FluentValidation for PlaceOrderCommand, request body size limit (1 MB → 413), malformed JSON → 400
- Architecture tests project (`Orders.Architecture.Tests`) enforcing Clean Architecture boundary rules via NetArchTest
- Integration tests project (`Orders.Integration.Tests`) with WebApplicationFactory, Testcontainers PostgreSQL, MassTransit InMemory test harness, and test auth bypass
- CI pipeline job: EF migrations validation (`has-pending-model-changes` + `database update` against temp PostgreSQL)
- Frontend: `ErrorBoundary` component at app root with `role="alert"` fallback
- Frontend: `useApiError` hook for ProblemDetails parsing and per-field validation error display
- Frontend: loading states with `aria-busy`, network error detection, and 401 auto-redirect to `/login`
- Frontend: environment-based API URL via `VITE_API_BASE_URL` with `/api` default
- Nginx reverse proxy: `/api` → `orders-api:8080`
- Property-based tests (FsCheck for C#, fast-check for TypeScript) validating 13 correctness properties across outbox, middleware, validation, CORS, rate limiting, security headers, and frontend error display
- ADR-006: API rate limiting and security headers
- ADR-007: Observability stack and correlation ID propagation

### Changed

- Middleware pipeline ordering: SecurityHeaders → CorrelationId → ExceptionHandler → CORS → RateLimiting → Authentication → Authorization → Routing
- OutboxMessage entity: added `RetryCount`, `FailedAt`, `FailureReason`, `CorrelationId` columns
- Outbox processor: query now filters `WHERE ProcessedAt IS NULL AND FailedAt IS NULL` with batch size from configuration
- Docker Compose: added `otel-collector`, `jaeger`, `prometheus` services; `orders-api` depends on `otel-collector`
- Frontend Dockerfile: accepts `VITE_API_BASE_URL` build argument

## [1.1.0] - 2025-07-20

### Added

- 10 Kiro steering skills for AI-assisted development guidance
  - Clean Architecture Layer Placement
  - DDD Aggregate & Entity Creation
  - CQRS Command/Query Scaffolding
  - MassTransit Consumer & Event Publishing
  - Minimal API Endpoint Conventions
  - EF Core Entity Configuration
  - Testing Conventions
  - Conventional Commits & PR Standards
  - React Feature Module
  - Docker & CI/CD Awareness
- Steering files included in template output (`.kiro/steering/`)
- README section documenting available steering skills

## [1.0.0] - 2026-07-20

### Added

- Initial template release
- Clean Architecture solution structure (Domain, Application, Infrastructure, API)
- CQRS with MediatR and pipeline behaviours (validation, logging, transaction)
- MassTransit + RabbitMQ messaging with transactional outbox pattern
- Entity Framework Core + PostgreSQL persistence
- OpenTelemetry observability (tracing, metrics) + Serilog structured logging
- FluentValidation request validation
- JWT Bearer authentication
- ASP.NET Core Minimal API endpoints
- MCP (Model Context Protocol) tooling with rate limiting and semantic caching
- Docker + Docker Compose local development environment
- Comprehensive test projects (unit, integration, architecture)
- Architecture Decision Records (ADRs)
- GitHub Actions CI/CD pipeline
- React 18 frontend with Vite, Zustand, and TanStack Query
