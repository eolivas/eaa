using System.Diagnostics;
using MediatR;
using Orders.Application.Interfaces;
using Orders.Domain;

namespace Orders.Application.Commands;

/// <summary>
/// Handles the PlaceOrderCommand by creating and placing an order,
/// persisting it, and publishing domain events.
/// </summary>
public class PlaceOrderHandler : IRequestHandler<PlaceOrderCommand, OrderId>
{
    private static readonly ActivitySource ActivitySource = new("Orders.Application");

    private readonly IOrderRepository _repo;
    private readonly IApplicationEventPublisher _publisher;

    public PlaceOrderHandler(IOrderRepository repo, IApplicationEventPublisher publisher)
    {
        _repo = repo;
        _publisher = publisher;
    }

    public async Task<OrderId> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("PlaceOrder");
        activity?.SetTag("customer.id", request.CustomerId.ToString());

        var lines = request.Lines
            .Select(l => OrderLine.Create(
                new ProductId(l.ProductId),
                l.Quantity,
                new Money(l.UnitPrice, l.Currency)))
            .ToList()
            .AsReadOnly();

        var order = Order.Create(new CustomerId(request.CustomerId), lines);
        order.Place();

        await _repo.SaveAsync(order, cancellationToken);

        foreach (var domainEvent in order.DomainEvents)
        {
            await _publisher.PublishAsync(domainEvent, cancellationToken);
        }

        order.ClearDomainEvents();

        return order.Id;
    }
}
