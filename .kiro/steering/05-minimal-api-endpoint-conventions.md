---
inclusion: auto
---

# Minimal API Endpoint Conventions

All HTTP endpoints are defined as Minimal APIs in `src/{SolutionName}.Api/Endpoints/`. Follow these conventions when adding new endpoints.

## Endpoint Group Structure

Each resource gets its own static class with a `Map{Resource}Endpoints` extension method:

```csharp
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using {SolutionName}.Application.Commands;
using {SolutionName}.Application.DTOs;
using {SolutionName}.Application.Queries;

namespace {SolutionName}.Api.Endpoints;

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
app.Map{Entity}Endpoints();
app.MapInvoicesEndpoints(); // Add new resource
```

## Rate Limiting

Endpoint groups that handle public traffic should apply rate limiting:

```csharp
var group = endpoints.MapGroup("/api/{entities}")
    .RequireAuthorization()
    .RequireRateLimiting("{solution-name}-api");  // Fixed-window rate limit
```

The `"{solution-name}-api"` rate limit policy is configured in `Add{SolutionName}RateLimiter()` extension. When exceeded, returns HTTP 429 with `Retry-After` header.

## Health Check & Operational Endpoints

These are registered separately from business endpoints:

| Endpoint | Purpose | Auth |
|----------|---------|------|
| `GET /health/live` | Liveness probe (always healthy) | Anonymous |
| `GET /health/ready` | Readiness (checks PostgreSQL + RabbitMQ) | Anonymous |
| `GET /openapi/v1.json` | OpenAPI spec | Anonymous |
| `GET /swagger` | Swagger UI (Development only) | Anonymous |

Do NOT add auth or rate limiting to health check endpoints.

## Endpoint Conventions

### Route Pattern
- Base: `/api/{resource}` (plural, lowercase)
- Item: `/api/{resource}/{id:guid}`
- Actions: `/api/{resource}/{id:guid}/{action}`

### Required Metadata
Every endpoint MUST include:
```csharp
group.MapPost("/", async (...) => { ... })
    .WithName("Place{Entity}")           // Unique operation name (PascalCase)
    .WithSummary("Places a new {entity}") // Short description
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
    var result = await sender.Send(new Get{Entity}Query(id));
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
public record Place{Entity}Request
{
    public Guid CustomerId { get; init; }
    public IReadOnlyList<Place{Entity}LineRequest> Lines { get; init; } = [];
}

public record Place{Entity}LineRequest
{
    public Guid ProductId { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
}

public record Cancel{Entity}Request
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
catch ({Entity}DomainException)
{
    return Results.Conflict();
}
```

`ValidationException` from FluentValidation is handled by the middleware and returns 400.
