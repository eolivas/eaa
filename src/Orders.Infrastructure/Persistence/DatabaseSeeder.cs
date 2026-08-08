using Microsoft.EntityFrameworkCore;
using Orders.Domain;
using Serilog;

namespace Orders.Infrastructure.Persistence;

/// <summary>
/// Seeds the database with sample orders in each lifecycle state for development environments.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(OrdersDbContext dbContext)
    {
        if (await dbContext.Orders.AnyAsync())
            return;

        // 1. Pending order (Create only)
        var pendingOrder = Order.Create(
            CustomerId.New(),
            new[]
            {
                OrderLine.Create(ProductId.New(), 2, new Money(29.99m, "USD")),
                OrderLine.Create(ProductId.New(), 1, new Money(49.99m, "USD"))
            });

        // 2. Placed order (Create → Place)
        var placedOrder = Order.Create(
            CustomerId.New(),
            new[]
            {
                OrderLine.Create(ProductId.New(), 1, new Money(99.99m, "USD")),
                OrderLine.Create(ProductId.New(), 3, new Money(12.50m, "USD")),
                OrderLine.Create(ProductId.New(), 1, new Money(75.00m, "USD"))
            });
        placedOrder.Place();

        // 3. Cancelled order (Create → Cancel)
        var cancelledOrder = Order.Create(
            CustomerId.New(),
            new[]
            {
                OrderLine.Create(ProductId.New(), 1, new Money(199.99m, "USD"))
            });
        cancelledOrder.Cancel("Customer changed their mind");

        // 4. Shipped order (Create, then set status directly via EF)
        var shippedOrder = Order.Create(
            CustomerId.New(),
            new[]
            {
                OrderLine.Create(ProductId.New(), 2, new Money(45.00m, "USD")),
                OrderLine.Create(ProductId.New(), 1, new Money(89.99m, "USD"))
            });

        dbContext.Orders.AddRange(pendingOrder, placedOrder, cancelledOrder, shippedOrder);

        // Set Shipped status directly since there is no Ship() domain method
        dbContext.Entry(shippedOrder).Property(nameof(Order.Status)).CurrentValue = OrderStatus.Shipped;

        await dbContext.SaveChangesAsync();

        Log.Information("Database seeded with {Count} orders.", 4);
    }
}
