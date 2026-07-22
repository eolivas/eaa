---
inclusion: auto
---

# CQRS Command/Query Scaffolding

This project uses MediatR for CQRS with FluentValidation and pipeline behaviours in `src/Orders.Application/`.

## Commands

### Command Definition (`Commands/{Name}Command.cs`)

```csharp
using MediatR;
using Orders.Domain;

namespace Orders.Application.Commands;

public record PlaceOrderCommand : IRequest<OrderId>
{
    public Guid CustomerId { get; init; }
    public IReadOnlyList<OrderLineDto> Lines { get; init; } = [];
}

// Inline DTO for command-specific input
public record OrderLineDto
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
}
```

Rules:
- Use `record` with `init` properties
- `IRequest<TResponse>` where TResponse is a strongly-typed ID or Unit
- Command-specific DTOs are co-located in the same file
- Naming: `{Verb}{Noun}Command`

### Command Validator (`Commands/{Name}CommandValidator.cs`)

```csharp
using FluentValidation;

namespace Orders.Application.Commands;

public class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.Lines)
            .NotEmpty()
            .WithMessage("An order must contain at least one line.");
    }
}
```

Rules:
- One validator per command (not required for queries)
- Class name: `{CommandName}Validator`
- Validators are auto-registered via `AddValidatorsFromAssembly`
- The `ValidationBehaviour<,>` pipeline catches failures and throws `ValidationException`

### Command Handler (`Commands/{Name}Handler.cs`)

```csharp
using MediatR;
using Orders.Application.Interfaces;
using Orders.Domain;

namespace Orders.Application.Commands;

public class PlaceOrderHandler : IRequestHandler<PlaceOrderCommand, OrderId>
{
    private readonly IOrderRepository _repo;
    private readonly IApplicationEventPublisher _publisher;

    public PlaceOrderHandler(IOrderRepository repo, IApplicationEventPublisher publisher)
    {
        _repo = repo;
        _publisher = publisher;
    }

    public async Task<OrderId> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        // 1. Map DTOs to domain objects
        // 2. Create/load aggregate and invoke domain behaviour
        // 3. Persist via repository
        // 4. Publish domain events
        // 5. Return result
    }
}
```

Rules:
- Handler class name: `{Verb}{Noun}Handler`
- Inject repository interfaces (from Domain) and application interfaces (from Application/Interfaces)
- Never inject infrastructure types directly
- Publish domain events after persistence

## Queries

### Query Definition (`Queries/{Name}Query.cs`)

```csharp
using MediatR;
using Orders.Application.DTOs;

namespace Orders.Application.Queries;

public record GetOrderQuery(Guid OrderId) : IRequest<OrderDto?>;
```

Rules:
- Use positional `record` parameters for simple queries
- Return DTOs (from `DTOs/`), never domain entities
- Naming: `Get{Noun}Query` or `List{Noun}Query`
- Nullable return (`OrderDto?`) when the item might not exist

### Query Handler (`Queries/{Name}Handler.cs`)

```csharp
public class GetOrderHandler : IRequestHandler<GetOrderQuery, OrderDto?>
{
    private readonly IOrderRepository _repo;

    public GetOrderHandler(IOrderRepository repo)
    {
        _repo = repo;
    }

    public async Task<OrderDto?> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await _repo.GetByIdAsync(new OrderId(request.OrderId), cancellationToken);
        return OrderDto.From(order);
    }
}
```

## Response DTOs (`DTOs/`)

```csharp
public record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal TotalAmount,
    string TotalCurrency,
    IReadOnlyList<OrderLineDto> Lines)
{
    public static OrderDto? From(Order? order) { /* mapping logic */ }
}
```

Rules:
- Use positional `record` constructors
- Include a static `From(DomainEntity?)` mapping method
- Place in `src/Orders.Application/DTOs/`

## Pipeline Behaviours (`Behaviours/`)

Already wired in `Program.cs`:
1. `LoggingBehaviour<,>` — logs request/response
2. `ValidationBehaviour<,>` — runs FluentValidation validators

To add a new behaviour, create it in `Behaviours/` and register in `Program.cs`:
```csharp
cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(YourBehaviour<,>));
```

## File Placement Summary

```
src/Orders.Application/
├── Commands/
│   ├── {Verb}{Noun}Command.cs       (command + inline DTOs)
│   ├── {Verb}{Noun}CommandValidator.cs
│   └── {Verb}{Noun}Handler.cs
├── Queries/
│   ├── {Get/List}{Noun}Query.cs
│   └── {Get/List}{Noun}Handler.cs
├── DTOs/
│   └── {Noun}Dto.cs
├── Behaviours/
│   └── {Name}Behaviour.cs
└── Interfaces/
    └── I{ServiceName}.cs
```
