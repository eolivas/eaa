using Orders.Domain;

namespace Orders.Application.Interfaces;

/// <summary>
/// Write-side operations for the aggregate (ISP: separated from read concerns).
/// </summary>
public interface IOrderWriter
{
    /// <summary>
    /// Creates a new aggregate instance.
    /// </summary>
    Task<OrderId> PlaceOrder(CustomerId customerId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an existing aggregate.
    /// </summary>
    Task CancelOrder(OrderId orderId, string reason, CancellationToken cancellationToken = default);
}
