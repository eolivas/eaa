using Orders.Domain;

namespace Orders.Application.Interfaces;

/// <summary>
/// Read-side operations for orders (ISP: separated from write and export concerns).
/// </summary>
public interface IOrderReader
{
    /// <summary>
    /// Retrieves an order by its identifier.
    /// </summary>
    Task<Order?> GetOrder(OrderId orderId, CancellationToken cancellationToken = default);
}
