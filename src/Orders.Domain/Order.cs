using Orders.Domain.Common;
using Orders.Domain.Events;
using Orders.Domain.Exceptions;

namespace Orders.Domain;

/// <summary>
/// The Order aggregate root. Manages the lifecycle of an order from creation through
/// placement, shipping, or cancellation.
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
    /// Creates a new Order in Pending status.
    /// </summary>
    /// <param name="customerId">The customer placing the order.</param>
    /// <param name="lines">The order lines. Must not be null or empty.</param>
    /// <returns>A new <see cref="Order"/> instance.</returns>
    /// <exception cref="OrderDomainException">Thrown when <paramref name="lines"/> is null or empty.</exception>
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
    /// Places the order. Only valid when status is Pending.
    /// </summary>
    /// <exception cref="OrderDomainException">Thrown when the order is not in Pending status.</exception>
    public void Place()
    {
        if (Status != OrderStatus.Pending)
            throw new OrderDomainException("Only a pending order can be placed.");

        Status = OrderStatus.Placed;
        RaiseDomainEvent(new OrderPlacedEvent(Id));
    }

    /// <summary>
    /// Cancels the order with a reason. Cannot cancel shipped or already-cancelled orders.
    /// </summary>
    /// <param name="reason">The cancellation reason.</param>
    /// <exception cref="OrderDomainException">Thrown when the order is shipped or already cancelled.</exception>
    public void Cancel(string reason)
    {
        if (Status is OrderStatus.Shipped or OrderStatus.Cancelled)
            throw new OrderDomainException("Cannot cancel an order that is shipped or already cancelled.");

        Status = OrderStatus.Cancelled;
        RaiseDomainEvent(new OrderCancelledEvent(Id, reason));
    }
}
