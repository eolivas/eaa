using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Orders.Infrastructure.Persistence;

/// <summary>
/// EF Core entity type configuration for <see cref="OutboxMessage"/>.
/// Maps to the <c>outbox_messages</c> table with non-nullable columns for Id, EventType, Payload, OccurredAt
/// and a nullable ProcessedAt column.
/// </summary>
public sealed class OutboxMessageEntityTypeConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.EventType)
            .IsRequired();

        builder.Property(m => m.Payload)
            .IsRequired();

        builder.Property(m => m.OccurredAt)
            .IsRequired();

        builder.Property(m => m.ProcessedAt)
            .IsRequired(false);
    }
}
