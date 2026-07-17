using Microsoft.EntityFrameworkCore;
using Orders.Domain;

namespace Orders.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IOrderRepository"/>.
/// Eager-loads OrderLine entities on every query via Include.
/// </summary>
public sealed class EfOrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _context;

    public EfOrderRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include("_lines")
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetByCustomerAsync(CustomerId customerId, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include("_lines")
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(Order order, CancellationToken cancellationToken = default)
    {
        var entry = _context.Entry(order);

        if (entry.State == EntityState.Detached)
        {
            _context.Orders.Add(order);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Order order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Remove(order);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
