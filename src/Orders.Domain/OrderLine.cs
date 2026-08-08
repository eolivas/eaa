using Orders.Domain.Common;
using Orders.Domain.Exceptions;

namespace Orders.Domain;

/// <summary>
/// Child entity within the aggregate. Replace with your domain's line item or child concept.
/// Demonstrates: entity with factory method, invariant enforcement, computed properties.
/// </summary>
public class OrderLine : Entity<OrderLineId>
{
    public ProductId ProductId { get; private init; }
    public int Quantity { get; private init; }
    public Money UnitPrice { get; private init; } = default!;
    public Money LineTotal => UnitPrice * Quantity;

    private OrderLine() { }

    /// <summary>
    /// Factory method enforcing child entity invariants.
    /// </summary>
    public static OrderLine Create(ProductId productId, int quantity, Money unitPrice)
    {
        if (quantity <= 0)
            throw new OrderDomainException("Order line quantity must be greater than zero.");

        return new OrderLine
        {
            Id = OrderLineId.New(),
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice
        };
    }
}
