---
inclusion: auto
---

# DDD Aggregate & Entity Creation

Follow these patterns when creating new aggregates, entities, and value objects in `src/Orders.Domain/`.

## Strongly-Typed IDs

Every entity and aggregate uses a strongly-typed ID implemented as a `readonly record struct`:

```csharp
namespace Orders.Domain;

public readonly record struct ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.NewGuid());
}
```

Rules:
- Place in the root of `Orders.Domain/` (e.g., `InvoiceId.cs`)
- Always provide a static `New()` factory method
- Use `readonly record struct` for value semantics and zero-allocation equality

## Entity Base Class

All entities inherit from `Entity<TId>`:

```csharp
public class OrderLine : Entity<OrderLineId>
{
    public ProductId ProductId { get; private init; }
    public int Quantity { get; private init; }
    // ...
}
```

Rules:
- Use `private init` or `private set` for all properties (no public setters)
- Use a private parameterless constructor for EF Core: `private OrderLine() { }`
- Create instances via static factory methods, never public constructors

## Aggregate Root

Aggregates inherit from `AggregateRoot<TId>` and are the only entities that raise domain events:

```csharp
public class Invoice : AggregateRoot<InvoiceId>
{
    private readonly List<InvoiceLine> _lines = [];

    public CustomerId CustomerId { get; private init; }
    public InvoiceStatus Status { get; private set; }
    public IReadOnlyList<InvoiceLine> Lines => _lines.AsReadOnly();

    private Invoice() { }

    public static Invoice Create(CustomerId customerId, IReadOnlyList<InvoiceLine> lines)
    {
        // Validate invariants
        if (lines is null || lines.Count == 0)
            throw new InvoiceDomainException("An invoice must have at least one line.");

        var invoice = new Invoice
        {
            Id = InvoiceId.New(),
            CustomerId = customerId,
            Status = InvoiceStatus.Draft
        };

        invoice._lines.AddRange(lines);
        invoice.RaiseDomainEvent(new InvoiceCreatedEvent(invoice.Id, customerId));

        return invoice;
    }
}
```

Rules:
- Private parameterless constructor
- Public static `Create(...)` factory method with invariant validation
- Private backing field `List<T>` exposed as `IReadOnlyList<T>`
- Call `RaiseDomainEvent(...)` for state transitions
- Throw domain-specific exceptions (e.g., `OrderDomainException`) for invariant violations

## Value Objects

Use C# `record` types for value objects with validation in the constructor:

```csharp
public record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(string currency) => new(0m, currency);
}
```

Rules:
- Place in `src/Orders.Domain/ValueObjects/`
- Validate in constructor, throw `ArgumentException` for invalid input
- Provide static factory methods for common cases (e.g., `Zero()`)
- Operator overloads where arithmetic makes sense

## Domain Events

```csharp
public sealed record InvoiceCreatedEvent(InvoiceId InvoiceId, CustomerId CustomerId) : DomainEvent;
```

Rules:
- Place in `src/Orders.Domain/Events/`
- `sealed record` inheriting from `DomainEvent`
- Name pattern: `{Aggregate}{PastTenseVerb}Event`
- Carry only IDs and minimal data needed by consumers

## Domain Exceptions

```csharp
public class InvoiceDomainException : Exception
{
    public InvoiceDomainException(string message) : base(message) { }
}
```

Rules:
- Place in `src/Orders.Domain/Exceptions/`
- One exception class per aggregate (or shared `OrderDomainException` style)
- Used for business rule violations, not infrastructure errors
