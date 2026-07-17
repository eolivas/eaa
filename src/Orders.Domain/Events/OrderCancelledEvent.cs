using Orders.Domain.Common;

namespace Orders.Domain.Events;

/// <summary>
/// Raised when an order is cancelled.
/// </summary>
public sealed record OrderCancelledEvent(OrderId OrderId, string Reason) : DomainEvent;
