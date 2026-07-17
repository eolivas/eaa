using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace Orders.Api.Mcp;

/// <summary>
/// Middleware that applies a per-user fixed-window rate limit to MCP endpoints.
/// Non-MCP requests pass through immediately.
/// </summary>
public class McpRateLimiterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly McpRateLimitOptions _options;
    private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> _limiters = new();

    public McpRateLimiterMiddleware(RequestDelegate next, IOptions<McpRateLimitOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to MCP endpoints
        if (!context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            // No authenticated user — pass through (auth middleware will handle rejection)
            await _next(context);
            return;
        }

        var limiter = _limiters.GetOrAdd(userId, _ => CreateLimiter());

        using var lease = await limiter.AcquireAsync(
            permitCount: 1,
            cancellationToken: context.RequestAborted);

        if (lease.IsAcquired)
        {
            await _next(context);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            }
            else
            {
                // Fallback: suggest retry after the full window
                context.Response.Headers.RetryAfter = _options.WindowSeconds.ToString();
            }

            await context.Response.WriteAsync("Rate limit exceeded. Try again later.");
        }
    }

    private static string? GetUserId(HttpContext context)
    {
        return context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
    }

    private FixedWindowRateLimiter CreateLimiter()
    {
        return new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = _options.PermitLimit,
            Window = TimeSpan.FromSeconds(_options.WindowSeconds),
            QueueLimit = _options.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    }
}
