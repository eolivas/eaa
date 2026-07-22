# Changelog

All notable changes to this template will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
