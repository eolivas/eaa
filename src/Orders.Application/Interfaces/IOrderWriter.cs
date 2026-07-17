using Orders.Domain;

namespace Orders.Application.Interfaces;

/// <summary>
/// Write-side operations for orders (ISP: separated from read and export concerns).
/// </summary>
public interface IOrderWriter
{
    /// <summary>
    /// Places a new order for the specified customer with the given lines.
    /// </summary>
    Task<OrderId> PlaceOrder(CustomerId customerId, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an existing order.
    /// </summary>
    Task CancelOrder(OrderId orderId, string reason, CancellationToken cancellationToken = default);
}
