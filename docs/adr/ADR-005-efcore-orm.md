# ADR-005: Selection of EF Core 8 as the ORM

## Status

Accepted

## Context

The Orders service requires a persistence layer to map domain aggregates (`Order`, `OrderLine`) and supporting entities (`OutboxMessage`) to a relational database. Key requirements:

- **Domain model fidelity**: The ORM must support mapping rich domain models with value objects (e.g., `Money`), strongly-typed IDs (`OrderId`, `CustomerId`), private collection fields, and owned entity types — without forcing the domain to conform to database conventions.
- **Migration support**: Schema changes must be version-controlled and applied in CI/CD pipelines.
- **Provider portability**: The platform targets PostgreSQL in production (Docker Compose and AWS RDS Aurora) and must support SQLite or in-memory providers for integration testing.
- **Security**: All queries must use parameterized inputs. Raw SQL string concatenation with user-supplied input is prohibited (Requirement 18.4).
- **Performance**: The ORM must support eager loading (`.Include()`), projection queries, and compiled queries for hot paths.

Alternatives considered:
- **Dapper**: Lightweight and fast but requires manual mapping, no change tracking, no migration tooling, and no built-in support for complex aggregate mapping.
- **NHibernate**: Mature but declining community activity, heavier configuration, and less alignment with modern .NET idioms.
- **Raw ADO.NET**: Maximum control but prohibitive maintenance cost for complex domain models.

## Decision

We select **Entity Framework Core 8** as the ORM for all .NET services in the platform. The implementation in the Orders service:

- **DbContext**: `OrdersDbContext` manages `DbSet<Order>` (mapped as `orders` table) and `DbSet<OutboxMessage>` (mapped as `outbox_messages` table).
- **Entity Type Configurations**: Fluent API configurations in separate `IEntityTypeConfiguration<T>` classes:
  - `OrderEntityTypeConfiguration`: Maps `Order` aggregate with strongly-typed ID conversions (`OrderId` → `Guid`), `Money` as an owned entity (no separate table), and `OrderLine` as an owned collection in `order_lines` table with orphan deletion and `PropertyAccessMode.Field` targeting the private `_lines` field.
  - `OutboxMessageEntityTypeConfiguration`: Maps `OutboxMessage` with non-nullable columns and nullable `ProcessedAt` for polling.
- **Repository pattern**: `EfOrderRepository` implements `IOrderRepository` (defined in Domain) using EF Core. All queries use `.Include(o => o.Lines)` to ensure aggregates are always loaded complete.
- **Parameterized queries only**: All EF Core LINQ queries compile to parameterized SQL. No raw SQL string concatenation appears anywhere in the codebase.
- **Database provider**: PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL` in production and Docker Compose; SQLite in-memory for unit/integration tests.

## Consequences

### Positive

- **Rich mapping capabilities**: Owned entities, value conversions, private field access, and shadow properties allow faithful mapping of DDD aggregates without polluting the domain model.
- **Migration tooling**: `dotnet ef migrations` provides version-controlled, reviewable schema changes that integrate naturally with CI/CD.
- **LINQ-to-SQL**: Type-safe queries with compile-time checking, eliminating SQL injection risk by default (all queries are parameterized).
- **Ecosystem integration**: First-party support for OpenTelemetry instrumentation, health checks, and ASP.NET Core DI registration.
- **Testing flexibility**: `UseInMemoryDatabase` or SQLite in-memory allows fast integration tests without external database dependencies.

### Negative

- **Abstraction leakage**: Complex queries sometimes require understanding EF Core's query translation behavior (e.g., client-side evaluation warnings, N+1 query patterns).
- **Change tracker overhead**: For read-heavy workloads, the change tracker adds memory overhead. `AsNoTracking()` must be applied manually for query-only paths.
- **Migration conflicts**: Concurrent schema changes by multiple developers can produce migration merge conflicts requiring manual resolution.
- **Provider differences**: Subtle behavioral differences between PostgreSQL (production) and SQLite (tests) can mask issues that only surface in production.
