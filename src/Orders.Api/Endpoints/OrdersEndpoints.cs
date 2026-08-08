using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Orders.Api.Extensions;

namespace Orders.Api.Endpoints;

/// <summary>
/// Minimal API endpoint definitions for the Orders resource.
/// Replace with your domain-specific endpoints after scaffolding.
/// </summary>
public static class OrdersEndpoints
{
    /// <summary>
    /// Maps all Orders endpoints under /api/orders with authorization required.
    /// </summary>
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/orders")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingServiceCollectionExtensions.PolicyName);

        group.MapPost("/", (CreateRequest request, ISender sender) =>
        {
            // TODO: Replace with your domain command
            // var command = new CreateYourEntityCommand { ... };
            // var id = await sender.Send(command);
            // return Results.Created($"/api/orders/{id}", new { id });

            return Results.StatusCode(StatusCodes.Status501NotImplemented);
        })
        .WithName("Create")
        .WithSummary("Creates a new resource")
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .WithOpenApi();

        group.MapGet("/{id:guid}", (Guid id, ISender sender) =>
        {
            // TODO: Replace with your domain query
            // var result = await sender.Send(new GetYourEntityQuery(id));
            // return result is not null ? Results.Ok(result) : Results.NotFound();

            return Results.StatusCode(StatusCodes.Status501NotImplemented);
        })
        .WithName("GetById")
        .WithSummary("Gets a resource by ID")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        group.MapDelete("/{id:guid}", (Guid id, ISender sender) =>
        {
            // TODO: Replace with your domain command
            // var command = new DeleteYourEntityCommand { Id = id };
            // await sender.Send(command);
            // return Results.NoContent();

            return Results.StatusCode(StatusCodes.Status501NotImplemented);
        })
        .WithName("Delete")
        .WithSummary("Deletes a resource by ID")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        return endpoints;
    }
}

/// <summary>
/// Example request body. Replace with your domain-specific properties.
/// </summary>
public record CreateRequest
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Request body for creating a resource with line items.
/// Used by validation tests. Replace with your domain-specific request model.
/// </summary>
public record PlaceOrderRequest
{
    public Guid CustomerId { get; init; }
    public IReadOnlyList<PlaceOrderLineRequest> Lines { get; init; } = [];
}

/// <summary>
/// Represents a single line item in a creation request.
/// </summary>
public record PlaceOrderLineRequest
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
}

/// <summary>
/// Request body for cancelling/deleting a resource.
/// </summary>
public record CancelOrderRequest
{
    public string Reason { get; init; } = string.Empty;
}
