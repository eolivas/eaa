using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Persistence;

namespace Orders.Infrastructure.Tests.Outbox;

/// <summary>
/// Property-based tests for outbox batch ordering and size.
/// Validates: Requirements 2.1
/// </summary>
public class OutboxBatchOrderingPropertyTests
{
    /// <summary>
    /// For any set of unprocessed outbox messages with distinct OccurredAt timestamps,
    /// the outbox batch query SHALL retrieve at most batchSize messages and they SHALL
    /// be ordered by OccurredAt ascending (earliest first).
    /// </summary>
    [Property(
        DisplayName = "Feature: template-architecture-gaps, Property 1: Outbox Batch Ordering and Size",
        MaxTest = 100)]
    public Property Batch_returns_at_most_batchSize_messages_ordered_by_OccurredAt()
    {
        return Prop.ForAll(
            GenerateDistinctOutboxMessages(),
            GenerateBatchSize(),
            (messages, batchSize) =>
            {
                // Arrange: create in-memory DbContext with the generated messages
                var options = new DbContextOptionsBuilder<OrdersDbContext>()
                    .UseInMemoryDatabase(databaseName: $"OutboxBatchTest_{Guid.NewGuid()}")
                    .Options;

                using var dbContext = new OrdersDbContext(options);
                dbContext.OutboxMessages.AddRange(messages);
                dbContext.SaveChanges();

                // Act: execute the same query the OutboxProcessor uses
                var result = dbContext.OutboxMessages
                    .Where(m => m.ProcessedAt == null && m.FailedAt == null)
                    .OrderBy(m => m.OccurredAt)
                    .Take(batchSize)
                    .ToList();

                // Assert 1: result count is at most batchSize
                var countWithinLimit = result.Count <= batchSize;

                // Assert 2: result count is the minimum of batchSize and total unprocessed messages
                var unprocessedCount = messages.Count(m => m.ProcessedAt == null && m.FailedAt == null);
                var expectedCount = Math.Min(batchSize, unprocessedCount);
                var correctCount = result.Count == expectedCount;

                // Assert 3: results are ordered by OccurredAt ascending
                var isOrdered = true;
                for (int i = 1; i < result.Count; i++)
                {
                    if (result[i].OccurredAt < result[i - 1].OccurredAt)
                    {
                        isOrdered = false;
                        break;
                    }
                }

                return countWithinLimit && correctCount && isOrdered;
            });
    }

    private static Arbitrary<List<OutboxMessage>> GenerateDistinctOutboxMessages()
    {
        var gen = Gen.Choose(1, 50).SelectMany(count =>
        {
            // Generate a base timestamp and create distinct timestamps by adding unique offsets
            return Gen.ListOf(count, Arb.Generate<int>()).Select(offsets =>
            {
                var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var distinctOffsets = offsets
                    .Select((_, idx) => idx)  // Use index to guarantee distinct offsets
                    .ToList();

                // Shuffle to randomize order of insertion
                var rng = new System.Random(offsets.GetHashCode());
                var shuffled = distinctOffsets.OrderBy(_ => rng.Next()).ToList();

                return shuffled.Select(offset => new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = "TestEvent",
                    Payload = "{}",
                    OccurredAt = baseTime.AddMinutes(offset),
                    ProcessedAt = null,
                    FailedAt = null,
                    RetryCount = 0
                }).ToList();
            });
        });

        return Arb.From(gen);
    }

    private static Arbitrary<int> GenerateBatchSize()
    {
        // Generate batch sizes between 1 and 30 to cover various scenarios
        return Arb.From(Gen.Choose(1, 30));
    }
}
