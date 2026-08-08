using Orders.Domain;

namespace Orders.Application.Interfaces;

/// <summary>
/// Read-side operations for the aggregate (ISP: separated from write concerns).
/// </summary>
public interface IOrderReader
{
    /// <summary>
    /// Retrieves an aggregate by its identifier.
    /// </summary>
    Task<Order?> GetOrder(OrderId orderId, CancellationToken cancellationToken = default);
}
