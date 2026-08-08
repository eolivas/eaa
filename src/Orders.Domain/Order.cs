using Orders.Domain.Common;
using Orders.Domain.Events;
using Orders.Domain.Exceptions;

namespace Orders.Domain;

/// <summary>
/// The Orders aggregate root. Replace with your domain entity and business rules.
/// Demonstrates: aggregate root pattern, domain events, state transitions, and invariant enforcement.
/// </summary>
public class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderLine> _lines = [];

    public CustomerId CustomerId { get; private init; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    /// <summary>
    /// Computed total: sum of all line totals.
    /// </summary>
    public Money Total => _lines.Aggregate(
        Money.Zero(_lines[0].UnitPrice.Currency),
        (sum, line) => sum + line.LineTotal);

    private Order() { }

    /// <summary>
    /// Factory method for creating a new aggregate instance.
    /// Enforces invariants: must have at least one line item.
    /// </summary>
    public static Order Create(CustomerId customerId, IReadOnlyList<OrderLine> lines)
    {
        if (lines is null || lines.Count == 0)
            throw new OrderDomainException("An order must contain at least one line.");

        var order = new Order
        {
            Id = OrderId.New(),
            CustomerId = customerId,
            Status = OrderStatus.Pending
        };

        order._lines.AddRange(lines);
        order.RaiseDomainEvent(new OrderCreatedEvent(order.Id, customerId));

        return order;
    }

    /// <summary>
    /// Transitions the aggregate to the next state.
    /// Demonstrates state machine pattern with domain event emission.
    /// </summary>
    public void Place()
    {
        if (Status != OrderStatus.Pending)
            throw new OrderDomainException("Only a pending order can be placed.");

        Status = OrderStatus.Placed;
        RaiseDomainEvent(new OrderPlacedEvent(Id));
    }

    /// <summary>
    /// Cancels the aggregate with a reason. Demonstrates guarded state transitions.
    /// </summary>
    public void Cancel(string reason)
    {
        if (Status is OrderStatus.Shipped or OrderStatus.Cancelled)
            throw new OrderDomainException("Cannot cancel an order that is shipped or already cancelled.");

        Status = OrderStatus.Cancelled;
        RaiseDomainEvent(new OrderCancelledEvent(Id, reason));
    }
}
