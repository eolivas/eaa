using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orders.Infrastructure.Configuration;
using Orders.Infrastructure.Persistence;

namespace Orders.Infrastructure.Messaging;

/// <summary>
/// Background service that periodically removes processed outbox messages
/// older than the configured retention period. Deletes in batches to avoid
/// long-running transactions and lock contention.
/// </summary>
public class OutboxRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRetentionService> _logger;
    private readonly OutboxRetentionOptions _options;

    public OutboxRetentionService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxRetentionService> logger,
        IOptions<OutboxRetentionOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(_options.IntervalMinutes);
        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupExpiredMessagesAsync(stoppingToken);
        }
    }

    private async Task CleanupExpiredMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var retentionCutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
        var batchSize = _options.BatchSize;
        var totalDeleted = 0;

        while (true)
        {
            var batch = await dbContext.OutboxMessages
                .Where(m => m.ProcessedAt != null && m.ProcessedAt < retentionCutoff)
                .OrderBy(m => m.ProcessedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            dbContext.OutboxMessages.RemoveRange(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
            totalDeleted += batch.Count;
        }

        if (totalDeleted > 0)
        {
            _logger.LogInformation(
                "Outbox retention cleanup completed: deleted {DeletedCount} messages older than {RetentionDays} days",
                totalDeleted,
                _options.RetentionDays);
        }
    }
}
