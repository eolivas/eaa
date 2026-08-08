using Orders.Domain.Common;

namespace Orders.Domain.Events;

/// <summary>
/// Domain event raised when the aggregate is created.
/// </summary>
public sealed record OrderCreatedEvent(OrderId OrderId, CustomerId CustomerId) : DomainEvent;
