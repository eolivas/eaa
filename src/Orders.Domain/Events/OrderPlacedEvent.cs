using Orders.Domain.Common;

namespace Orders.Domain.Events;

/// <summary>
/// Raised when an order transitions to Placed status.
/// </summary>
public sealed record OrderPlacedEvent(OrderId OrderId) : DomainEvent;
