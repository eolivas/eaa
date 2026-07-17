using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orders.Infrastructure.Persistence;

namespace Orders.Infrastructure.Messaging;

/// <summary>
/// Background service that polls the outbox_messages table every 5 seconds,
/// deserialises persisted domain events, publishes them via MassTransit,
/// and marks each message as processed in the same scope.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessOutboxMessagesAsync(stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.EventType);
                if (eventType is null)
                {
                    _logger.LogError(
                        "OutboxMessage {OutboxMessageId}: unable to resolve event type '{EventType}'",
                        message.Id,
                        message.EventType);
                    continue;
                }

                var domainEvent = JsonSerializer.Deserialize(message.Payload, eventType);
                if (domainEvent is null)
                {
                    _logger.LogError(
                        "OutboxMessage {OutboxMessageId}: deserialisation returned null for type '{EventType}'",
                        message.Id,
                        message.EventType);
                    continue;
                }

                await publishEndpoint.Publish(domainEvent, eventType, cancellationToken);

                message.ProcessedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "OutboxMessage {OutboxMessageId}: failed to process",
                    message.Id);

                // Detach the entity so the failed ProcessedAt assignment is discarded,
                // leaving ProcessedAt null for retry on the next polling cycle.
                dbContext.Entry(message).State = EntityState.Unchanged;
            }
        }
    }
}
