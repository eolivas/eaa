using Orders.Infrastructure.Messaging;

namespace Orders.Api.Services;

/// <summary>
/// Reads the correlation ID from HttpContext.Items, set by CorrelationIdMiddleware.
/// Returns null when called outside an HTTP context (e.g., from background services).
/// </summary>
public sealed class HttpContextCorrelationIdAccessor : ICorrelationIdAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? CorrelationId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Items.TryGetValue("CorrelationId", out var value) == true)
            {
                return value as string;
            }
            return null;
        }
    }
}
