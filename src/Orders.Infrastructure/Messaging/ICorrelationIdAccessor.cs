namespace Orders.Infrastructure.Messaging;

/// <summary>
/// Provides access to the current correlation ID.
/// Allows the DbContext to capture the correlation ID when creating outbox messages
/// without depending directly on IHttpContextAccessor (which is unavailable in background services).
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>
    /// Gets the current correlation ID, or null if not available.
    /// </summary>
    string? CorrelationId { get; }
}
