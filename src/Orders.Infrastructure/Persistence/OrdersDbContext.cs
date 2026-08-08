using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orders.Domain;
using Orders.Domain.Common;
using Orders.Infrastructure.Messaging;

namespace Orders.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the bounded context.
/// Intercepts domain events during SaveChangesAsync and persists them as OutboxMessage rows
/// within the same transaction as the aggregate changes (transactional outbox pattern).
/// </summary>
public class OrdersDbContext : DbContext
{
    private readonly ICorrelationIdAccessor? _correlationIdAccessor;

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options)
        : base(options)
    {
    }

    public OrdersDbContext(
        DbContextOptions<OrdersDbContext> options,
        ICorrelationIdAccessor? correlationIdAccessor)
        : base(options)
    {
        _correlationIdAccessor = correlationIdAccessor;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregatesWithEvents = ChangeTracker.Entries<AggregateRoot<OrderId>>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = aggregatesWithEvents
            .SelectMany(a => a.DomainEvents)
            .ToList();

        var correlationId = _correlationIdAccessor?.CorrelationId;

        foreach (var domainEvent in domainEvents)
        {
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = domainEvent.GetType().AssemblyQualifiedName!,
                Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                OccurredAt = domainEvent.OccurredAt,
                ProcessedAt = null,
                CorrelationId = correlationId
            };

            OutboxMessages.Add(outboxMessage);
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }

        return result;
    }
}
