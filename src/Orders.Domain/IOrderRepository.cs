namespace Orders.Domain;

/// <summary>
/// Repository interface for the aggregate root.
/// Implementation resides in the Infrastructure layer (Dependency Inversion Principle).
/// </summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetByCustomerAsync(CustomerId customerId, CancellationToken cancellationToken = default);

    Task SaveAsync(Order order, CancellationToken cancellationToken = default);

    Task DeleteAsync(Order order, CancellationToken cancellationToken = default);
}
