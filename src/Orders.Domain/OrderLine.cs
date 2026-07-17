using Orders.Domain.Common;
using Orders.Domain.Exceptions;

namespace Orders.Domain;

/// <summary>
/// Represents a single line item within an order.
/// </summary>
public class OrderLine : Entity<OrderLineId>
{
    public ProductId ProductId { get; private init; }
    public int Quantity { get; private init; }
    public Money UnitPrice { get; private init; } = default!;
    public Money LineTotal => UnitPrice * Quantity;

    private OrderLine() { }

    /// <summary>
    /// Creates a new <see cref="OrderLine"/> with the specified product, quantity, and unit price.
    /// </summary>
    /// <param name="productId">The product identifier.</param>
    /// <param name="quantity">The quantity ordered. Must be greater than zero.</param>
    /// <param name="unitPrice">The price per unit.</param>
    /// <returns>A new <see cref="OrderLine"/> instance.</returns>
    /// <exception cref="OrderDomainException">Thrown when <paramref name="quantity"/> is less than or equal to zero.</exception>
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
