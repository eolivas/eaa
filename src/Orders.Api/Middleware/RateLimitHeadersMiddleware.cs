using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
using Orders.Api.Extensions;
using Orders.Infrastructure.Configuration;

namespace Orders.Api.Middleware;

/// <summary>
/// Middleware that adds X-RateLimit-Limit and X-RateLimit-Remaining response headers
/// for successful requests to rate-limited /api/orders endpoints.
/// Must be placed after UseRateLimiter() in the pipeline so that rejected requests
/// (429) are already handled before reaching this middleware.
/// </summary>
public class RateLimitHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly int _permitLimit;
    private readonly int _windowSeconds;
    private readonly ConcurrentDictionary<string, WindowCounter> _counters = new();

    public RateLimitHeadersMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;

        var rateLimitOptions = new RateLimitOptions();
        configuration.GetSection("RateLimit").Bind(rateLimitOptions);
        _permitLimit = rateLimitOptions.PermitLimit;
        _windowSeconds = rateLimitOptions.WindowSeconds;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/orders"))
        {
            await _next(context);
            return;
        }

        // Determine the partition key (same logic as the rate limiter policy)
        var partitionKey = context.User?.FindFirstValue("sub")
            ?? context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";

        // Track request count within the current window
        var now = DateTimeOffset.UtcNow;
        var counter = _counters.AddOrUpdate(
            partitionKey,
            _ => new WindowCounter(now, 1, _windowSeconds),
            (_, existing) => existing.Increment(now, _windowSeconds));

        var remaining = Math.Max(0, _permitLimit - counter.Count);

        context.Response.OnStarting(() =>
        {
            // Only add headers if the request was not rejected (not 429)
            if (context.Response.StatusCode != StatusCodes.Status429TooManyRequests)
            {
                context.Response.Headers["X-RateLimit-Limit"] =
                    _permitLimit.ToString(CultureInfo.InvariantCulture);
                context.Response.Headers["X-RateLimit-Remaining"] =
                    remaining.ToString(CultureInfo.InvariantCulture);
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }

    /// <summary>
    /// Tracks request count within a fixed time window for a single partition.
    /// </summary>
    private sealed class WindowCounter
    {
        private readonly object _lock = new();
        private DateTimeOffset _windowStart;
        private int _count;

        public int Count
        {
            get { lock (_lock) { return _count; } }
        }

        public WindowCounter(DateTimeOffset windowStart, int count, int windowSeconds)
        {
            _windowStart = windowStart;
            _count = count;
        }

        public WindowCounter Increment(DateTimeOffset now, int windowSeconds)
        {
            lock (_lock)
            {
                if ((now - _windowStart).TotalSeconds >= windowSeconds)
                {
                    // Window expired, reset
                    _windowStart = now;
                    _count = 1;
                }
                else
                {
                    _count++;
                }
            }

            return this;
        }
    }
}
