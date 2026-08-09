# ADR-011: Manual Object Mapping Over AutoMapper

## Status

Accepted

## Context

The application layer requires mapping between domain entities and DTOs (outbound) and between API request records and commands (inbound). This mapping occurs in every handler and endpoint. The mapping approach must:

- **Be debuggable**: Engineers must be able to step through mapping logic with standard IDE debugging tools.
- **Provide compile-time safety**: Missing or type-mismatched mappings must be caught at compile time, not at runtime.
- **Be discoverable**: "Find All References" and "Go to Definition" must work for every mapped property.
- **Minimize dependencies**: The Application layer should not take a dependency on a mapping library that may conflict with other libraries or require specific configuration.
- **Perform well**: Mapping is called on every request — zero unnecessary allocations or reflection.

Alternatives considered:

- **AutoMapper**: Most popular .NET mapping library. Profile-based configuration with convention-driven property matching. Provides `ProjectTo<T>()` for EF Core query projection.
  - Drawback: Runtime profile errors (missing maps discovered only at test/runtime), "magic" property matching that hides bugs, difficult to debug (can't step through the mapping), and `IMapper` injection adds a dependency to every handler.
- **Mapster**: Source-generator-based mapper with better performance than AutoMapper. Compile-time code generation.
  - Drawback: Generated code is harder to read/debug than hand-written code. Configuration API is less intuitive. Still adds a library dependency and learning curve.
- **Mapperly**: Pure source generator — generates mapping methods at compile time with zero runtime reflection.
  - Drawback: Requires `partial` method declarations and generates code in `obj/`. Debugging the generated code is non-trivial. Adds a build-time dependency.

## Decision

We use **manual static mapping methods** defined on the DTO type itself:

```csharp
public record {Entity}Dto(Guid Id, Guid CustomerId, string Status, decimal TotalAmount, string TotalCurrency)
{
    public static {Entity}Dto? From({Entity}? entity)
    {
        if (entity is null) return null;
        return new {Entity}Dto(
            entity.Id.Value,
            entity.CustomerId.Value,
            entity.Status.ToString(),
            entity.Total.Amount,
            entity.Total.Currency);
    }
}
```

### Implementation Conventions

- **DTO → static `From(DomainEntity?)` method**: Lives on the DTO class. Handles null. Maps all properties explicitly.
- **Request → Command**: Inline in the endpoint (trivial, 3-5 lines). No separate method unless > 10 lines.
- **Command → Domain**: Not mapping — calls domain factory methods (`{Entity}.Create(...)`). Domain validates invariants.
- **No mapping library dependency**: Zero NuGet packages for mapping in any layer.
- **No reflection at runtime**: Pure property assignments — identical performance to hand-written code (because it IS hand-written code).

### Scale Assessment

At the current project scale (~5-15 DTOs per service), manual mapping is approximately:
- 5-10 lines per DTO mapping method
- 50-150 total lines of mapping code per service
- Fully covered by the handler tests that exercise the mapping path

## Consequences

### Positive

- **Full IDE support**: "Go to Definition" on any mapped property navigates directly to the assignment. "Find All References" shows everywhere the property is used.
- **Compile-time safety**: If a domain entity property is renamed, the mapping method breaks at compile time — not at runtime in production.
- **Zero learning curve**: It's standard C# — no profile configuration, no conventions to learn, no mapping-specific debugging tools needed.
- **Zero runtime overhead**: No reflection, no expression compilation, no dictionary lookups. Just property assignments.
- **Debuggable**: Set a breakpoint on any line of the `From()` method and inspect values during debugging.
- **No hidden behaviour**: What you see is exactly what executes. No implicit null handling, no convention-based flattening, no surprising type conversions.

### Negative

- **Boilerplate for flat mappings**: Simple entity-to-DTO mappings with 10+ properties require more lines than AutoMapper's convention-based approach.
- **No `ProjectTo<T>()`**: AutoMapper's EF Core projection generates optimal SQL `SELECT` with only needed columns. Manual mapping requires writing `.Select()` projections explicitly for query optimization.
- **Maintenance on schema change**: Adding a property to both entity and DTO requires updating the `From()` method. AutoMapper's conventions would handle matching names automatically.
- **Repetitive for CRUD-heavy services**: Services with many similar DTOs write similar mapping code repeatedly. Mitigated by the DTO count being small per service (bounded context keeps it manageable).

### Re-evaluation Criteria

Revisit this decision if:
- A single service exceeds 30 DTO types with 15+ properties each
- Multiple engineers report mapping maintenance as a significant time cost
- A source-generator mapper (Mapperly) matures to provide full debugging support

Until then, the simplicity, debuggability, and compile-time safety of manual mapping outweigh the boilerplate cost.
