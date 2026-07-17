using MediatR;
using Orders.Domain;

namespace Orders.Application.Commands;

/// <summary>
/// Command to place a new order for a customer.
/// </summary>
public record PlaceOrderCommand : IRequest<OrderId>
{
    public Guid CustomerId { get; init; }
    public IReadOnlyList<OrderLineDto> Lines { get; init; } = [];
}

/// <summary>
/// DTO representing a single order line in the PlaceOrderCommand.
/// </summary>
public record OrderLineDto
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
}
