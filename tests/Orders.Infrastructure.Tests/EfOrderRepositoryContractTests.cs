using Orders.Domain;
using Orders.Domain.Tests;

namespace Orders.Infrastructure.Tests;

/// <summary>
/// In-memory implementation of the repository interface used to demonstrate
/// the Liskov Substitution Principle contract tests.
/// </summary>
public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<OrderId, Order> _store = new();

    public Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(id, out var order);
        return Task.FromResult(order);
    }

    public Task<IReadOnlyList<Order>> GetByCustomerAsync(CustomerId customerId, CancellationToken cancellationToken = default)
    {
        var orders = _store.Values.Where(o => o.CustomerId == customerId).ToList();
        return Task.FromResult<IReadOnlyList<Order>>(orders);
    }

    public Task SaveAsync(Order order, CancellationToken cancellationToken = default)
    {
        _store[order.Id] = order;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Order order, CancellationToken cancellationToken = default)
    {
        _store.Remove(order.Id);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Concrete contract tests for the in-memory repository implementation.
/// Extends the shared abstract test class to verify this implementation
/// satisfies the repository interface contract (Liskov Substitution Principle).
/// </summary>
public class InMemoryOrderRepositoryContractTests
    : OrderRepositoryContractTests<InMemoryOrderRepository>
{
    protected override InMemoryOrderRepository CreateRepository()
    {
        return new InMemoryOrderRepository();
    }
}
