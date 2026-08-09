# ADR-009: Testing Framework Selection

## Status

Accepted

## Context

The platform requires a comprehensive testing strategy spanning unit tests, property-based tests, architecture enforcement, and integration tests. The testing stack must support:

- **Fast feedback loops**: Unit tests must run in milliseconds to encourage frequent execution during development.
- **Property-based testing (PBT)**: Universal invariants (domain rules, serialization round-trips, middleware behaviour) must be verified against generated inputs, not just hand-picked examples.
- **Architecture enforcement**: Layer dependency rules must be validated at build time to prevent accidental violations from being merged.
- **Integration testing**: Full HTTP pipeline tests with real database instances must be possible without cloud infrastructure.
- **Frontend parity**: The React frontend needs an equivalent testing approach with fast execution and PBT support.

Alternatives considered:

- **NUnit**: Mature, widely used, but lacks native property-based testing integration and has a heavier assertion API.
- **MSTest**: Microsoft's framework, but less community ecosystem for advanced scenarios (PBT, architecture testing).
- **SpecFlow/Cucumber**: BDD-style tests are verbose for the granularity needed in domain testing and add Gherkin parsing overhead.
- **Hypothesis (Python-style PBT)**: Not available for .NET; FsCheck is the mature .NET equivalent.
- **Jest (frontend)**: Being replaced by Vitest in the Vite ecosystem; slower execution, less native ESM support.

## Decision

We select the following testing stack:

### Backend (.NET)

| Tool | Role |
|------|------|
| **xUnit** | Test framework — minimal ceremony, constructor-based DI, `[Fact]`/`[Theory]` attributes |
| **FsCheck.Xunit** | Property-based testing — generates arbitrary inputs, shrinks counterexamples |
| **FluentAssertions** | Assertion library for Application/Infrastructure tests (richer failure messages) |
| **Moq** | Mocking framework for handler tests (interface-based mocking) |
| **NetArchTest.Rules** | Architecture enforcement (dependency rule validation at build time) |
| **Bogus** | Test data generation (Faker pattern for complex aggregates) |
| **Testcontainers.PostgreSql** | Real PostgreSQL for integration tests (Docker-based) |
| **Microsoft.AspNetCore.Mvc.Testing** | WebApplicationFactory for full HTTP pipeline tests |
| **MassTransit.Testing** | InMemory test harness for message consumer assertions |

### Frontend (React/TypeScript)

| Tool | Role |
|------|------|
| **Vitest** | Test runner — native Vite integration, ESM-first, fast execution |
| **@testing-library/react** | Component testing with accessible queries |
| **@fast-check/vitest** | Property-based testing for frontend (equivalent to FsCheck) |
| **MSW (Mock Service Worker)** | API mocking at the network layer |

### Key Decisions

- **xUnit over NUnit/MSTest**: Simpler API, constructor-based fixture injection (no `[SetUp]`/`[TearDown]` ceremony), and first-class `IAsyncLifetime` for async setup/teardown. Community standard for modern .NET.
- **FsCheck for PBT**: Mature .NET PBT library with xUnit integration (`[Property]` attribute), custom generators (`Arb`), and automatic shrinking of counterexamples. Property tests verify invariants that example-based tests cannot cover exhaustively.
- **Domain tests use `Assert.*` directly**: No FluentAssertions in domain tests — keeps the Domain project dependency-free and tests minimal.
- **Vitest over Jest**: Native Vite integration (no babel transform), ESM-first, compatible test API, significantly faster execution for the frontend build toolchain.

## Consequences

### Positive

- **PBT catches edge cases**: FsCheck/fast-check discover boundary conditions that hand-picked examples miss (e.g., zero quantities, empty collections, Unicode strings, maximum values).
- **Architecture enforcement at build time**: NetArchTest runs in CI — layer violations are caught before merge, not during code review.
- **Fast unit tests**: Domain and handler tests run in < 1ms each, enabling test-driven development with instant feedback.
- **Realistic integration tests**: Testcontainers provides a real PostgreSQL instance — no SQLite behaviour differences masking production bugs.
- **Frontend testing parity**: Same PBT approach (fast-check) and accessible query philosophy across backend and frontend.

### Negative

- **FsCheck learning curve**: Property-based testing requires thinking in universal statements rather than specific examples. Engineers new to PBT need guidance (covered in steering files 07, 19).
- **Docker dependency for integration tests**: Testcontainers requires Docker Desktop running locally. Developers without Docker can only run unit tests.
- **Multiple assertion styles**: `Assert.*` in domain tests, FluentAssertions in application/infrastructure tests — two styles to learn (justified by keeping Domain dependency-free).
- **Test infrastructure maintenance**: WebApplicationFactory, Testcontainers setup, and MassTransit test harness add configuration code that must be maintained alongside production code.
