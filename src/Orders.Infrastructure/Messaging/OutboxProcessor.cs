using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orders.Infrastructure.Configuration;
using Orders.Infrastructure.Persistence;

namespace Orders.Infrastructure.Messaging;

/// <summary>
/// Background service that polls the outbox_messages table on a configurable interval,
/// deserialises persisted domain events in batches, publishes them via MassTransit,
/// and marks each message as processed. Messages that exceed the maximum retry count
/// are dead-lettered (FailedAt set, excluded from future polling).
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessor> _logger;
    private readonly OutboxOptions _options;

    private static readonly Meter s_meter = new("Orders.Infrastructure.Outbox", "1.0.0");
    private static readonly Counter<long> s_processedCounter = s_meter.CreateCounter<long>(
        "outbox.messages.processed",
        description: "Number of outbox messages successfully processed");
    private static readonly Counter<long> s_failedCounter = s_meter.CreateCounter<long>(
        "outbox.messages.failed",
        description: "Number of outbox messages that failed processing");
    private static readonly Histogram<double> s_durationHistogram = s_meter.CreateHistogram<double>(
        "outbox.message.duration_ms",
        unit: "ms",
        description: "Duration in milliseconds to process a single outbox message");

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessor> logger,
        IOptions<OutboxOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollingInterval = TimeSpan.FromSeconds(_options.PollingIntervalSeconds);
        using var timer = new PeriodicTimer(pollingInterval);

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

        var batchSize = _options.BatchSize;
        var maxRetries = _options.MaxRetries;

        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.FailedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var eventType = Type.GetType(message.EventType);
                if (eventType is null)
                {
                    _logger.LogError(
                        "OutboxMessage {OutboxMessageId}: unable to resolve event type '{EventType}'",
                        message.Id,
                        message.EventType);
                    HandleFailure(message, maxRetries, new InvalidOperationException($"Unable to resolve event type '{message.EventType}'"));
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

                var domainEvent = JsonSerializer.Deserialize(message.Payload, eventType);
                if (domainEvent is null)
                {
                    _logger.LogError(
                        "OutboxMessage {OutboxMessageId}: deserialisation returned null for type '{EventType}'",
                        message.Id,
                        message.EventType);
                    HandleFailure(message, maxRetries, new InvalidOperationException($"Deserialisation returned null for type '{message.EventType}'"));
                    await dbContext.SaveChangesAsync(cancellationToken);
                    continue;
                }

                await publishEndpoint.Publish(domainEvent, eventType, ctx =>
                {
                    if (!string.IsNullOrEmpty(message.CorrelationId))
                    {
                        ctx.Headers.Set("X-Correlation-Id", message.CorrelationId);
                    }
                }, cancellationToken);

                message.ProcessedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                stopwatch.Stop();
                s_processedCounter.Add(1);
                s_durationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(
                    ex,
                    "OutboxMessage {OutboxMessageId}: failed to process (RetryCount={RetryCount})",
                    message.Id,
                    message.RetryCount);

                HandleFailure(message, maxRetries, ex);
                await dbContext.SaveChangesAsync(cancellationToken);

                s_failedCounter.Add(1);
                s_durationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds);
            }
        }
    }

    private void HandleFailure(OutboxMessage message, int maxRetries, Exception ex)
    {
        message.RetryCount++;

        if (message.RetryCount >= maxRetries)
        {
            message.FailedAt = DateTime.UtcNow;
            message.FailureReason = ex.Message;

            _logger.LogWarning(
                "OutboxMessage {OutboxMessageId}: dead-lettered after {RetryCount} retries. Reason: {FailureReason}",
                message.Id,
                message.RetryCount,
                message.FailureReason);
        }
    }
}
