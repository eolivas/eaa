using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Infrastructure.Configuration;

namespace Orders.Api.Extensions;

/// <summary>
/// Extension methods for configuring rate limiting in the DI container.
/// Uses a fixed window policy partitioned by authenticated user or IP address.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    /// <summary>
    /// The rate limiting policy name applied to /api/orders endpoints.
    /// </summary>
    public const string PolicyName = "orders-api";

    /// <summary>
    /// HttpContext.Items key for storing the configured permit limit.
    /// </summary>
    internal const string PermitLimitKey = "RateLimit.PermitLimit";

    /// <summary>
    /// HttpContext.Items key for storing the remaining permits after a successful acquire.
    /// </summary>
    internal const string RemainingKey = "RateLimit.Remaining";

    /// <summary>
    /// Registers rate limiting services with a fixed window policy named "orders-api".
    /// Reads PermitLimit and WindowSeconds from the RateLimit configuration section.
    /// Partitions by authenticated user "sub" claim or falls back to RemoteIpAddress.
    /// On rejection: returns 429 with Retry-After header.
    /// On success: stores remaining permits in HttpContext.Items for header middleware.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration for reading rate limit settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOrdersRateLimiter(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rateLimitOptions = new RateLimitOptions();
        configuration.GetSection("RateLimit").Bind(rateLimitOptions);
        var permitLimit = rateLimitOptions.PermitLimit;
        var windowSeconds = rateLimitOptions.WindowSeconds;

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PolicyName, httpContext =>
            {
                var partitionKey = httpContext.User?.FindFirstValue("sub")
                    ?? httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                // Store the permit limit so response headers middleware can read it
                httpContext.Items[PermitLimitKey] = permitLimit;

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, key =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    var secondsRemaining = (int)Math.Ceiling(retryAfter.TotalSeconds);
                    context.HttpContext.Response.Headers.RetryAfter =
                        secondsRemaining.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    // Fallback: use configured window seconds as retry-after value
                    context.HttpContext.Response.Headers.RetryAfter =
                        windowSeconds.ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = 429,
                    detail = "Rate limit exceeded. Try again later."
                }, cancellationToken);
            };

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }
}
