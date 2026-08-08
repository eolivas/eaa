using MassTransit;
using Microsoft.Extensions.Logging;
using Orders.Domain.Events;

namespace Orders.Infrastructure.Messaging;

/// <summary>
/// Consumes domain events from the message broker and performs side effects.
/// Replace with your domain-specific event consumer logic.
/// Demonstrates: MassTransit consumer pattern with structured logging and correlation.
/// </summary>
public sealed class OrderPlacedConsumer : IConsumer<OrderPlacedEvent>
{
    private readonly ILogger<OrderPlacedConsumer> _logger;

    public OrderPlacedConsumer(ILogger<OrderPlacedConsumer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<OrderPlacedEvent> context)
    {
        var correlationId = context.Headers.Get<string>("X-Correlation-Id");

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId ?? Guid.NewGuid().ToString()))
        {
            _logger.LogInformation("Received OrderPlacedEvent for Order {OrderId}", context.Message.OrderId);
            await Task.CompletedTask;
        }
    }
}
