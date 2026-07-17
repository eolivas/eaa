namespace Orders.Domain.Common;

/// <summary>
/// Base record for all domain events. Captures when the event occurred.
/// </summary>
public abstract record DomainEvent
{
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
