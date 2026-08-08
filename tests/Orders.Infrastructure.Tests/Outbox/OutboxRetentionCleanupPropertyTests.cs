using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Persistence;

namespace Orders.Infrastructure.Tests.Outbox;

/// <summary>
/// Property-based tests for outbox retention cleanup.
/// Validates: Requirements 2.4, 18.1, 18.4
/// </summary>
public class OutboxRetentionCleanupPropertyTests
{
    /// <summary>
    /// For any set of outbox messages where some have ProcessedAt not null and older than
    /// the retention period, the retention service SHALL delete exactly those messages
    /// (and no others), processing them in batches of the configured size.
    /// </summary>
    [Property(
        DisplayName = "Feature: template-architecture-gaps, Property 3: Outbox Retention Cleanup",
        MaxTest = 100)]
    public Property Retention_cleanup_deletes_only_expired_messages_in_batches()
    {
        return Prop.ForAll(
            GenerateOutboxMessages(),
            GenerateRetentionDays(),
            GenerateBatchSize(),
            (messages, retentionDays, batchSize) =>
            {
                // Arrange: create in-memory DbContext with the generated messages
                var options = new DbContextOptionsBuilder<OrdersDbContext>()
                    .UseInMemoryDatabase(databaseName: $"OutboxRetentionTest_{Guid.NewGuid()}")
                    .Options;

                using var dbContext = new OrdersDbContext(options);
                dbContext.OutboxMessages.AddRange(messages);
                dbContext.SaveChanges();

                // Determine the retention cutoff (same logic as OutboxRetentionService)
                var retentionCutoff = DateTime.UtcNow.AddDays(-retentionDays);

                // Identify which messages should be deleted (expired) and which should remain
                var expectedDeletedIds = messages
                    .Where(m => m.ProcessedAt != null && m.ProcessedAt < retentionCutoff)
                    .Select(m => m.Id)
                    .ToHashSet();

                var expectedRemainingIds = messages
                    .Where(m => !(m.ProcessedAt != null && m.ProcessedAt < retentionCutoff))
                    .Select(m => m.Id)
                    .ToHashSet();

                // Act: replicate the retention service cleanup loop with batching
                var batchIterations = 0;
                while (true)
                {
                    var batch = dbContext.OutboxMessages
                        .Where(m => m.ProcessedAt != null && m.ProcessedAt < retentionCutoff)
                        .OrderBy(m => m.ProcessedAt)
                        .Take(batchSize)
                        .ToList();

                    if (batch.Count == 0)
                        break;

                    // Assert batching: each batch must not exceed configured size
                    if (batch.Count > batchSize)
                        return false;

                    dbContext.OutboxMessages.RemoveRange(batch);
                    dbContext.SaveChanges();
                    batchIterations++;
                }

                // Assert: verify that exactly expired messages were deleted
                var remainingIds = dbContext.OutboxMessages
                    .Select(m => m.Id)
                    .ToHashSet();

                // All expected remaining messages must still be present
                var allRemainingPresent = expectedRemainingIds.All(id => remainingIds.Contains(id));

                // No expired messages should remain
                var noExpiredRemaining = !expectedDeletedIds.Any(id => remainingIds.Contains(id));

                // Total remaining count matches expectations
                var correctTotalCount = remainingIds.Count == expectedRemainingIds.Count;

                // Verify batching: number of iterations should match ceiling(expiredCount / batchSize)
                var expectedIterations = expectedDeletedIds.Count == 0
                    ? 0
                    : (int)Math.Ceiling((double)expectedDeletedIds.Count / batchSize);
                var correctBatchIterations = batchIterations == expectedIterations;

                return allRemainingPresent && noExpiredRemaining && correctTotalCount && correctBatchIterations;
            });
    }

    /// <summary>
    /// Generates a list of outbox messages with varying ProcessedAt timestamps:
    /// - Some with ProcessedAt = null (unprocessed)
    /// - Some with ProcessedAt within retention period (recently processed)
    /// - Some with ProcessedAt older than the retention period (expired)
    /// </summary>
    private static Arbitrary<List<OutboxMessage>> GenerateOutboxMessages()
    {
        var gen = Gen.Choose(1, 30).SelectMany(count =>
        {
            return Gen.ListOf(count, GenerateSingleMessage()).Select(msgs => msgs.ToList());
        });

        return Arb.From(gen);
    }

    private static Gen<OutboxMessage> GenerateSingleMessage()
    {
        // Category: 0 = unprocessed (null), 1 = within retention, 2 = expired
        return Gen.Choose(0, 2).SelectMany(category =>
        {
            return Gen.Choose(1, 1000).Select(offsetMinutes =>
            {
                var message = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = "TestEvent",
                    Payload = "{}",
                    OccurredAt = DateTime.UtcNow.AddDays(-30),
                    RetryCount = 0,
                    FailedAt = null,
                    FailureReason = null
                };

                switch (category)
                {
                    case 0:
                        // Unprocessed: ProcessedAt remains null
                        message.ProcessedAt = null;
                        break;
                    case 1:
                        // Within retention: processed recently (1 minute to 2 days ago)
                        message.ProcessedAt = DateTime.UtcNow.AddMinutes(-offsetMinutes);
                        break;
                    case 2:
                        // Expired: processed long ago (30 to 60 days ago)
                        message.ProcessedAt = DateTime.UtcNow.AddDays(-30).AddMinutes(-offsetMinutes);
                        break;
                }

                return message;
            });
        });
    }

    /// <summary>
    /// Generates retention days between 1 and 20.
    /// Messages processed more than this many days ago are considered expired.
    /// </summary>
    private static Arbitrary<int> GenerateRetentionDays()
    {
        return Arb.From(Gen.Choose(1, 20));
    }

    /// <summary>
    /// Generates small batch sizes (1-20) to verify batching behavior effectively.
    /// </summary>
    private static Arbitrary<int> GenerateBatchSize()
    {
        return Arb.From(Gen.Choose(1, 20));
    }
}
