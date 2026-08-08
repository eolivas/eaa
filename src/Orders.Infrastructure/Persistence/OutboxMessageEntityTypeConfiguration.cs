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

        builder.Property(m => m.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(m => m.FailedAt)
            .IsRequired(false);

        builder.Property(m => m.FailureReason)
            .IsRequired(false);

        builder.Property(m => m.CorrelationId)
            .IsRequired(false)
            .HasMaxLength(36);

        // Filtered index for polling query: unprocessed messages that haven't failed
        builder.HasIndex(m => m.ProcessedAt)
            .HasDatabaseName("IX_outbox_messages_ProcessedAt_Polling")
            .HasFilter("\"FailedAt\" IS NULL");
    }
}
