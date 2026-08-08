using Serilog.Context;

namespace Orders.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ExtractOrGenerate(context.Request.Headers);
        context.Items["CorrelationId"] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationIdHeaderName] = correlationId;
                return Task.CompletedTask;
            });

            await _next(context);
        }
    }

    private static string ExtractOrGenerate(IHeaderDictionary headers)
    {
        if (headers.TryGetValue(CorrelationIdHeaderName, out var headerValue))
        {
            var value = headerValue.ToString();
            if (!string.IsNullOrWhiteSpace(value) && Guid.TryParse(value, out _))
            {
                return value;
            }
        }

        return Guid.NewGuid().ToString();
    }
}
