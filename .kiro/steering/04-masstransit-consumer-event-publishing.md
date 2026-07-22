---
inclusion: auto
---

# MassTransit Consumer & Event Publishing

This project uses MassTransit with an outbox pattern for reliable domain event delivery. All messaging infrastructure lives in `src/Orders.Infrastructure/Messaging/`.

## Architecture Overview

```
Domain Event raised in Aggregate
    ↓
Handler calls IApplicationEventPublisher.PublishAsync()
    ↓
MassTransitEventPublisher publishes via IPublishEndpoint
    ↓
MassTransit routes to registered consumers
```

The outbox pattern provides guaranteed delivery:
```
Domain Event → Serialized to outbox_messages table (same DB transaction)
    ↓
OutboxProcessor (BackgroundService) polls every 5s
    ↓
Deserializes and publishes via MassTransit
    ↓
Marks as processed
```

## Defining a New Domain Event

1. Create the event in `src/Orders.Domain/Events/`:

```csharp
using Orders.Domain.Common;

namespace Orders.Domain.Events;

public sealed record OrderShippedEvent(OrderId OrderId, DateTime ShippedAt) : DomainEvent;
```

2. Raise it from the aggregate:

```csharp
public void Ship()
{
    if (Status != OrderStatus.Placed)
        throw new OrderDomainException("Only placed orders can be shipped.");

    Status = OrderStatus.Shipped;
    RaiseDomainEvent(new OrderShippedEvent(Id, DateTime.UtcNow));
}
```

## Publishing Events (Application Layer)

The handler publishes events after persisting:

```csharp
await _repo.SaveAsync(order, cancellationToken);

foreach (var domainEvent in order.DomainEvents)
{
    await _publisher.PublishAsync(domainEvent, cancellationToken);
}

order.ClearDomainEvents();
```

The `IApplicationEventPublisher` interface lives in `Application/Interfaces/`. The implementation (`MassTransitEventPublisher`) lives in Infrastructure.

## Creating a New Consumer

Place in `src/Orders.Infrastructure/Messaging/`:

```csharp
using MassTransit;
using Microsoft.Extensions.Logging;
using Orders.Domain.Events;

namespace Orders.Infrastructure.Messaging;

public sealed class OrderShippedConsumer : IConsumer<OrderShippedEvent>
{
    private readonly ILogger<OrderShippedConsumer> _logger;

    public OrderShippedConsumer(ILogger<OrderShippedConsumer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Consume(ConsumeContext<OrderShippedEvent> context)
    {
        _logger.LogInformation(
            "Received OrderShippedEvent for Order {OrderId}",
            context.Message.OrderId);

        // Process the event (send notification, update read model, etc.)
        return Task.CompletedTask;
    }
}
```

Rules:
- Class name: `{EventName without "Event"}Consumer` (e.g., `OrderShippedConsumer`)
- `sealed class` implementing `IConsumer<TEvent>`
- Inject dependencies via constructor
- Use structured logging with message templates

## MassTransit Registration

In `Program.cs`, consumers are auto-discovered via `ConfigureEndpoints`:

```csharp
builder.Services.AddMassTransit(x =>
{
    x.UsingInMemory((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});
```

For production with RabbitMQ, switch to:
```csharp
x.UsingRabbitMq((context, cfg) =>
{
    cfg.Host(config["RabbitMq:Host"], h =>
    {
        h.Username(config["RabbitMq:Username"]);
        h.Password(config["RabbitMq:Password"]);
    });
    cfg.ConfigureEndpoints(context);
});
```

## Idempotency Expectations

- Consumers MUST be idempotent — the same event may be delivered more than once
- Use deduplication (check if already processed) or make operations naturally idempotent
- The outbox marks messages as processed after successful publish, but consumers should not assume exactly-once delivery

## Outbox Pattern

The `OutboxProcessor` background service:
- Polls `outbox_messages` table every 5 seconds
- Deserializes the event payload using `System.Text.Json`
- Publishes via MassTransit `IPublishEndpoint`
- Marks `ProcessedAt` on success
- On failure, detaches the entity state so it retries on next cycle

The `OutboxMessage` entity and its EF configuration live in `Persistence/`.
