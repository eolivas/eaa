using Microsoft.AspNetCore.Mvc;

namespace Orders.Api.Middleware;

public sealed class RequestBodySizeLimitMiddleware
{
    private const long MaxRequestBodySize = 1_048_576; // 1 MB

    private readonly RequestDelegate _next;

    public RequestBodySizeLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength > MaxRequestBodySize)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            context.Response.ContentType = "application/problem+json";

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = "Payload Too Large",
                Detail = $"Request body exceeds the maximum allowed size of {MaxRequestBodySize} bytes.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.14"
            };

            await context.Response.WriteAsJsonAsync(problemDetails);
            return;
        }

        await _next(context);
    }
}
