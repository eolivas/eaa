# Changelog

All notable changes to this template will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Clean Architecture solution structure (Domain, Application, Infrastructure, API)
- CQRS-ready with MediatR pipeline behaviours (validation, logging)
- FluentValidation integration
- JWT Bearer authentication
- ASP.NET Core Minimal API skeleton with health checks
- Security headers middleware
- Correlation ID middleware
- Request body size limit middleware
- OpenTelemetry observability (tracing, metrics) + Serilog structured logging
- Comprehensive test project structure (unit, integration, architecture)
- GitHub Actions CI/CD pipeline
- Docker + Docker Compose local development environment

## [1.0.0] - 2026-07-20

### Added

- Initial template release
- Clean Architecture solution structure
- Project scaffolding via `dotnet new eaa-solution`
