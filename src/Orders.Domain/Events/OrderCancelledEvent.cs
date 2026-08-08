using Orders.Domain.Common;

namespace Orders.Domain.Events;

/// <summary>
/// Domain event raised when the aggregate is cancelled.
/// </summary>
public sealed record OrderCancelledEvent(OrderId OrderId, string Reason) : DomainEvent;
