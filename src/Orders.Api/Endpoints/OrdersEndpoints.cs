using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Orders.Application.Commands;
using Orders.Application.DTOs;
using Orders.Application.Queries;
using Orders.Domain.Exceptions;

using CommandOrderLineDto = Orders.Application.Commands.OrderLineDto;

namespace Orders.Api.Endpoints;

/// <summary>
/// Minimal API endpoint definitions for the Orders resource.
/// </summary>
public static class OrdersEndpoints
{
    /// <summary>
    /// Maps all Orders endpoints under /api/orders with authorization required.
    /// </summary>
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/orders")
            .RequireAuthorization();

        group.MapPost("/", async (PlaceOrderRequest request, ISender sender) =>
        {
            var command = new PlaceOrderCommand
            {
                CustomerId = request.CustomerId,
                Lines = request.Lines.Select(l => new CommandOrderLineDto
                {
                    ProductId = l.ProductId,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    Currency = l.Currency
                }).ToList()
            };

            var id = await sender.Send(command);

            return Results.Created($"/api/orders/{id.Value}", new { id = id.Value });
        })
        .WithName("PlaceOrder")
        .WithSummary("Places a new order")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .WithOpenApi();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var order = await sender.Send(new GetOrderQuery(id));

            return order is not null
                ? Results.Ok(order)
                : Results.NotFound();
        })
        .WithName("GetOrder")
        .WithSummary("Gets an order by ID")
        .Produces<OrderDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        group.MapDelete("/{id:guid}", async (Guid id, CancelOrderRequest? request, ISender sender) =>
        {
            try
            {
                var command = new CancelOrderCommand
                {
                    OrderId = id,
                    Reason = request?.Reason ?? string.Empty
                };

                await sender.Send(command);

                return Results.NoContent();
            }
            catch (OrderDomainException)
            {
                return Results.Conflict();
            }
        })
        .WithName("CancelOrder")
        .WithSummary("Cancels an existing order")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status409Conflict)
        .WithOpenApi();

        return endpoints;
    }
}

/// <summary>
/// Request body for placing a new order.
/// </summary>
public record PlaceOrderRequest
{
    public Guid CustomerId { get; init; }
    public IReadOnlyList<PlaceOrderLineRequest> Lines { get; init; } = [];
}

/// <summary>
/// Represents a single line in a place order request.
/// </summary>
public record PlaceOrderLineRequest
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
}

/// <summary>
/// Request body for cancelling an order.
/// </summary>
public record CancelOrderRequest
{
    public string Reason { get; init; } = string.Empty;
}
