using MassTransit;
using Microsoft.Extensions.Logging;
using Orders.Domain.Events;

namespace Orders.Infrastructure.Messaging;

/// <summary>
/// Consumes <see cref="OrderPlacedEvent"/> messages and logs receipt (PoC stub).
/// In a production system this would trigger notification delivery.
/// </summary>
public sealed class OrderPlacedConsumer : IConsumer<OrderPlacedEvent>
{
    private readonly ILogger<OrderPlacedConsumer> _logger;

    public OrderPlacedConsumer(ILogger<OrderPlacedConsumer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        _logger.LogInformation("Received OrderPlacedEvent for Order {OrderId}", context.Message.OrderId);
        return Task.CompletedTask;
    }
}
