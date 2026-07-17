using MediatR;
using Orders.Application.DTOs;
using Orders.Domain;

namespace Orders.Application.Queries;

/// <summary>
/// Handles <see cref="GetOrderQuery"/> by retrieving the order from the repository.
/// </summary>
public class GetOrderHandler : IRequestHandler<GetOrderQuery, OrderDto?>
{
    private readonly IOrderRepository _repo;

    public GetOrderHandler(IOrderRepository repo)
    {
        _repo = repo;
    }

    public async Task<OrderDto?> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await _repo.GetByIdAsync(new OrderId(request.OrderId), cancellationToken);

        if (order is null)
            return null;

        return OrderDto.From(order);
    }
}
