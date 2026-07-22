---
inclusion: auto
---

# Minimal API Endpoint Conventions

All HTTP endpoints are defined as Minimal APIs in `src/Orders.Api/Endpoints/`. Follow these conventions when adding new endpoints.

## Endpoint Group Structure

Each resource gets its own static class with a `Map{Resource}Endpoints` extension method:

```csharp
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Orders.Application.Commands;
using Orders.Application.DTOs;
using Orders.Application.Queries;

namespace Orders.Api.Endpoints;

public static class InvoicesEndpoints
{
    public static IEndpointRouteBuilder MapInvoicesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/invoices")
            .RequireAuthorization();

        // Define endpoints on `group`...

        return endpoints;
    }
}
```

Register in `Program.cs`:
```csharp
app.MapOrdersEndpoints();
app.MapInvoicesEndpoints(); // Add new resource
```

## Endpoint Conventions

### Route Pattern
- Base: `/api/{resource}` (plural, lowercase)
- Item: `/api/{resource}/{id:guid}`
- Actions: `/api/{resource}/{id:guid}/{action}`

### Required Metadata
Every endpoint MUST include:
```csharp
group.MapPost("/", async (...) => { ... })
    .WithName("PlaceOrder")           // Unique operation name (PascalCase)
    .WithSummary("Places a new order") // Short description
    .Produces(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest)
    .WithOpenApi();                    // OpenAPI metadata generation
```

### Authorization
- `RequireAuthorization()` on the group (all endpoints require auth by default)
- For public endpoints, override with `.AllowAnonymous()`

### Dispatching via MediatR
Inject `ISender sender` (not `IMediator`) and dispatch:
```csharp
group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
{
    var result = await sender.Send(new GetOrderQuery(id));
    return result is not null ? Results.Ok(result) : Results.NotFound();
});
```

## HTTP Status Code Conventions

| Operation | Success | Client Error | Conflict |
|-----------|---------|--------------|----------|
| POST (create) | 201 Created | 400 Bad Request | 409 Conflict |
| GET (single) | 200 OK | — | — |
| GET (list) | 200 OK | — | — |
| PUT (update) | 200 OK or 204 No Content | 400 Bad Request | 409 Conflict |
| DELETE (soft) | 204 No Content | — | 409 Conflict |

- Return `Results.NotFound()` when a requested entity does not exist
- Return `Results.Conflict()` when a domain exception indicates invalid state transition

## Request/Response Records

Co-locate request/response records at the bottom of the endpoint file:

```csharp
public record PlaceOrderRequest
{
    public Guid CustomerId { get; init; }
    public IReadOnlyList<PlaceOrderLineRequest> Lines { get; init; } = [];
}

public record PlaceOrderLineRequest
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
}

public record CancelOrderRequest
{
    public string Reason { get; init; } = string.Empty;
}
```

Rules:
- Use `record` types with `init` properties
- Request suffix: `{Verb}{Noun}Request`
- These are API-level DTOs, distinct from Application-layer DTOs

## Error Handling

Domain exceptions are caught in the endpoint or by `ExceptionHandlingMiddleware`:
```csharp
try
{
    await sender.Send(command);
    return Results.NoContent();
}
catch (OrderDomainException)
{
    return Results.Conflict();
}
```

`ValidationException` from FluentValidation is handled by the middleware and returns 400.
