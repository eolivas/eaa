---
inclusion: auto
---

# EF Core Entity Configuration

All entity type configurations live in `src/Orders.Infrastructure/Persistence/`. Follow these patterns for new entities.

## Configuration Class Structure

One `IEntityTypeConfiguration<T>` per aggregate root:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain;

namespace Orders.Infrastructure.Persistence;

public sealed class OrderEntityTypeConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        // Configuration here
    }
}
```

Rules:
- File name: `{Entity}EntityTypeConfiguration.cs`
- `sealed class`
- Applied via `modelBuilder.ApplyConfigurationsFromAssembly(...)` in DbContext

## Table Naming

Use **snake_case** for table names:

```csharp
builder.ToTable("orders");
// builder.ToTable("order_lines");
// builder.ToTable("outbox_messages");
```

## Strongly-Typed ID Conversions

Every strongly-typed ID needs a value conversion:

```csharp
builder.HasKey(o => o.Id);
builder.Property(o => o.Id)
    .HasConversion(
        id => id.Value,
        value => new OrderId(value));

builder.Property(o => o.CustomerId)
    .HasConversion(
        id => id.Value,
        value => new CustomerId(value));
```

## Value Objects as Owned Entities

Map value objects using `OwnsOne`:

```csharp
lineBuilder.OwnsOne(l => l.UnitPrice, moneyBuilder =>
{
    moneyBuilder.Property(m => m.Amount)
        .HasColumnName("UnitPrice_Amount")
        .IsRequired();

    moneyBuilder.Property(m => m.Currency)
        .HasColumnName("UnitPrice_Currency")
        .HasMaxLength(3)
        .IsRequired();
});
```

Column naming pattern: `{PropertyName}_{ValueObjectProperty}`.

## Private Collections (Owned Many)

For child entity collections accessed via private backing fields:

```csharp
// Map the collection
builder.OwnsMany<OrderLine>("_lines", lineBuilder =>
{
    lineBuilder.ToTable("order_lines");
    lineBuilder.WithOwner().HasForeignKey("OrderId");
    lineBuilder.HasKey(l => l.Id);
    // ...configure properties...
});

// Configure field access mode
builder.Navigation("_lines")
    .UsePropertyAccessMode(PropertyAccessMode.Field);
```

Rules:
- Use the string name of the backing field `"_lines"` in `OwnsMany`
- Set `PropertyAccessMode.Field` on the navigation
- This allows EF Core to hydrate the private `List<T>` directly

## Computed/Derived Properties

Ignore properties computed from other data:

```csharp
builder.Ignore(o => o.Total);
lineBuilder.Ignore(l => l.LineTotal);
```

## Enum Conversions

Store enums as strings:

```csharp
builder.Property(o => o.Status)
    .HasConversion<string>();
```

## DbContext Registration

```csharp
public class OrdersDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
    }
}
```

## Checklist for New Entity Configuration

1. Create `{Entity}EntityTypeConfiguration.cs` in `Persistence/`
2. Set table name in snake_case
3. Add value conversions for all strongly-typed IDs
4. Map value objects with `OwnsOne`
5. Map child collections with `OwnsMany` + `PropertyAccessMode.Field`
6. Ignore computed properties
7. Store enums as strings
8. Add `DbSet<T>` to `OrdersDbContext` if it's a new aggregate root
