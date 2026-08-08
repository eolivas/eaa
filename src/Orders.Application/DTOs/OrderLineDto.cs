namespace Orders.Application.DTOs;

/// <summary>
/// Data transfer object representing a child entity in the aggregate.
/// </summary>
public record OrderLineDto(
    Guid Id,
    Guid ProductId,
    int Quantity,
    decimal UnitPriceAmount,
    string UnitPriceCurrency,
    decimal LineTotalAmount,
    string LineTotalCurrency);
