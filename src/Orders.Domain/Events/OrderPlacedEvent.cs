using Orders.Domain.Common;

namespace Orders.Domain.Events;

/// <summary>
/// Domain event raised when the aggregate transitions to its active/confirmed state.
/// </summary>
public sealed record OrderPlacedEvent(OrderId OrderId) : DomainEvent;
