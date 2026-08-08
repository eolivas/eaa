using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain;

namespace Orders.Infrastructure.Persistence;

/// <summary>
/// EF Core entity type configuration for the aggregate root.
/// Demonstrates: strongly-typed ID conversions, owned value objects, and owned-many collections.
/// Replace table/column names and mappings with your domain-specific schema.
/// </summary>
public sealed class OrderEntityTypeConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasConversion(
                id => id.Value,
                value => new OrderId(value));

        builder.Property(o => o.CustomerId)
            .HasConversion(
                id => id.Value,
                value => new CustomerId(value));

        builder.Property(o => o.Status)
            .HasConversion<string>();

        builder.Ignore(o => o.Total);
        builder.Ignore(o => o.Lines);
        builder.Ignore(o => o.DomainEvents);

        builder.OwnsMany<OrderLine>("_lines", lineBuilder =>
        {
            lineBuilder.ToTable("order_lines");
            lineBuilder.WithOwner().HasForeignKey("OrderId");

            lineBuilder.HasKey(l => l.Id);
            lineBuilder.Property(l => l.Id)
                .HasConversion(
                    id => id.Value,
                    value => new OrderLineId(value));

            lineBuilder.Property(l => l.ProductId)
                .HasConversion(
                    id => id.Value,
                    value => new ProductId(value));

            lineBuilder.Property(l => l.Quantity)
                .IsRequired();

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

            lineBuilder.Ignore(l => l.LineTotal);
        });

        builder.Navigation("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
