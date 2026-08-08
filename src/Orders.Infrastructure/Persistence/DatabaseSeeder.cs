using Microsoft.EntityFrameworkCore;
using Orders.Domain;
using Serilog;

namespace Orders.Infrastructure.Persistence;

/// <summary>
/// Seeds the database with sample data for development environments.
/// Replace with your domain-specific seed data.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(OrdersDbContext dbContext)
    {
        if (await dbContext.Orders.AnyAsync())
            return;

        // Example: Create sample aggregates in different lifecycle states
        var pendingOrder = Order.Create(
            CustomerId.New(),
            new[]
            {
                OrderLine.Create(ProductId.New(), 2, new Money(29.99m, "USD")),
                OrderLine.Create(ProductId.New(), 1, new Money(49.99m, "USD"))
            });

        var placedOrder = Order.Create(
            CustomerId.New(),
            new[]
            {
                OrderLine.Create(ProductId.New(), 1, new Money(99.99m, "USD"))
            });
        placedOrder.Place();

        dbContext.Orders.AddRange(pendingOrder, placedOrder);
        await dbContext.SaveChangesAsync();

        Log.Information("Database seeded with {Count} sample records.", 2);
    }
}
