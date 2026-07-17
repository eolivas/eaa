using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain;

namespace Orders.Infrastructure.Persistence;

/// <summary>
/// EF Core entity type configuration for the Order aggregate root.
/// Maps the Order to the "orders" table with strongly-typed ID conversions,
/// owned Money value objects, and owned-many OrderLine collection.
/// </summary>
public sealed class OrderEntityTypeConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        // Primary key with strongly-typed OrderId conversion
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasConversion(
                id => id.Value,
                value => new OrderId(value));

        // CustomerId with strongly-typed conversion
        builder.Property(o => o.CustomerId)
            .HasConversion(
                id => id.Value,
                value => new CustomerId(value));

        // OrderStatus stored as string
        builder.Property(o => o.Status)
            .HasConversion<string>();

        // Ignore the computed Total property — it derives from OrderLine data
        builder.Ignore(o => o.Total);

        // OrderLine collection as owned-many, mapped to "order_lines" table
        builder.OwnsMany<OrderLine>("_lines", lineBuilder =>
        {
            lineBuilder.ToTable("order_lines");

            // Use PropertyAccessMode.Field to access the private _lines backing field
            lineBuilder.WithOwner().HasForeignKey("OrderId");

            // OrderLineId with strongly-typed conversion
            lineBuilder.HasKey(l => l.Id);
            lineBuilder.Property(l => l.Id)
                .HasConversion(
                    id => id.Value,
                    value => new OrderLineId(value));

            // ProductId with strongly-typed conversion
            lineBuilder.Property(l => l.ProductId)
                .HasConversion(
                    id => id.Value,
                    value => new ProductId(value));

            // Quantity as required
            lineBuilder.Property(l => l.Quantity)
                .IsRequired();

            // UnitPrice (Money) as owned entity within OrderLine
            lineBuilder.OwnsOne(l => l.UnitPrice, moneyBuilder =>
            {
                moneyBuilder.Property(m => m.Amount)
                    .HasColumnName("UnitPrice_Amount")
                    .IsRequired();

                moneyBuilder.Property(m => m.Currency)
                    .HasColumnName("UnitPrice_Currency")
                    .HasMaxLength(3)
                    .IsRequired();
            });

            // Ignore the computed LineTotal property
            lineBuilder.Ignore(l => l.LineTotal);
        });

        // Configure PropertyAccessMode.Field for the _lines navigation
        builder.Navigation("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
