using MediatR;
using Orders.Domain;
using Orders.Domain.Exceptions;

namespace Orders.Application.Commands;

/// <summary>
/// Handles the CancelOrderCommand by retrieving the order,
/// applying the cancellation, and persisting the change.
/// </summary>
public class CancelOrderHandler : IRequestHandler<CancelOrderCommand, Unit>
{
    private readonly IOrderRepository _repo;

    public CancelOrderHandler(IOrderRepository repo)
    {
        _repo = repo;
    }

    public async Task<Unit> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await _repo.GetByIdAsync(new OrderId(command.OrderId), cancellationToken);

        if (order is null)
            throw new OrderDomainException("Order not found.");

        order.Cancel(command.Reason);

        await _repo.SaveAsync(order, cancellationToken);

        return Unit.Value;
    }
}
