using MediatR;
using Orders.Domain;

namespace Orders.Application.Commands;

/// <summary>
/// Command to create a new aggregate instance.
/// Replace with your domain-specific creation command.
/// </summary>
public record PlaceOrderCommand : IRequest<OrderId>
{
    public Guid CustomerId { get; init; }
    public IReadOnlyList<OrderLineDto> Lines { get; init; } = [];
}

/// <summary>
/// DTO representing a child item in the creation command.
/// </summary>
public record OrderLineDto
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
}
