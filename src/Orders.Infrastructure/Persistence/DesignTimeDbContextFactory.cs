using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orders.Infrastructure.Persistence;

/// <summary>
/// Factory used by EF Core CLI tooling (dotnet ef migrations) to create an OrdersDbContext
/// at design time without requiring the API host project.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrdersDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=orders_db;Username=postgres;Password=postgres");

        return new OrdersDbContext(optionsBuilder.Options);
    }
}
