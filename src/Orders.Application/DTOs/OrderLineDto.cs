using Orders.Domain;

namespace Orders.Application.DTOs;

/// <summary>
/// Data transfer object representing an order line.
/// </summary>
public record OrderLineDto(
    Guid Id,
    Guid ProductId,
    int Quantity,
    decimal UnitPriceAmount,
    string UnitPriceCurrency,
    decimal LineTotalAmount,
    string LineTotalCurrency);
