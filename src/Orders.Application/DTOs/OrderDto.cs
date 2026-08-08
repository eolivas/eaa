using Orders.Domain;

namespace Orders.Application.DTOs;

/// <summary>
/// Data transfer object for the aggregate root.
/// Replace properties with your domain-specific fields.
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
    /// Maps the domain aggregate to its DTO representation.
    /// </summary>
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
