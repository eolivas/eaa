namespace Orders.Infrastructure.Persistence;

/// <summary>
/// Represents a domain event persisted to the outbox table for reliable eventual publishing.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
