using Orders.Domain.Common;

namespace Orders.Domain.Events;

/// <summary>
/// Raised when a new order is created.
/// </summary>
public sealed record OrderCreatedEvent(OrderId OrderId, CustomerId CustomerId) : DomainEvent;
