using Orders.Domain;

namespace Orders.Application.DTOs;

/// <summary>
/// Data transfer object representing an order.
/// </summary>
public record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal TotalAmount,
    string TotalCurrency,
    IReadOnlyList<OrderLineDto> Lines)
{
    /// <summary>
    /// Maps a domain Order to an OrderDto.
    /// </summary>
    /// <param name="order">The domain order. May be null.</param>
    /// <returns>An <see cref="OrderDto"/> or null if the order is null.</returns>
    public static OrderDto? From(Order? order)
    {
        if (order is null)
            return null;

        var lines = order.Lines.Select(line => new OrderLineDto(
            line.Id.Value,
            line.ProductId.Value,
            line.Quantity,
            line.UnitPrice.Amount,
            line.UnitPrice.Currency,
            line.LineTotal.Amount,
            line.LineTotal.Currency)).ToList();

        return new OrderDto(
            order.Id.Value,
            order.CustomerId.Value,
            order.Status.ToString(),
            order.Total.Amount,
            order.Total.Currency,
            lines);
    }
}
