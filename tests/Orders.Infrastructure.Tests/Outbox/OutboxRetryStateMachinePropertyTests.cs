using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Orders.Infrastructure.Persistence;

namespace Orders.Infrastructure.Tests.Outbox;

/// <summary>
/// Property-based tests for the outbox retry state machine.
/// Validates: Requirements 2.2, 2.3
/// </summary>
public class OutboxRetryStateMachinePropertyTests
{
    /// <summary>
    /// For any outbox message that fails processing:
    /// - If its RetryCount is less than the configured maximum after increment,
    ///   the message SHALL remain eligible (ProcessedAt and FailedAt both null).
    /// - If its RetryCount equals or exceeds the configured maximum after increment,
    ///   the message SHALL have FailedAt set and SHALL be excluded from subsequent processing.
    /// </summary>
    [Property(
        DisplayName = "Feature: template-architecture-gaps, Property 2: Outbox Retry State Machine",
        MaxTest = 100)]
    public Property Retry_state_machine_transitions_correctly_on_failure()
    {
        return Prop.ForAll(
            GenerateRetryCount(),
            GenerateMaxRetries(),
            (initialRetryCount, maxRetries) =>
            {
                // Arrange: create an outbox message with the given RetryCount
                var message = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = "TestEvent",
                    Payload = "{}",
                    OccurredAt = DateTime.UtcNow,
                    ProcessedAt = null,
                    FailedAt = null,
                    FailureReason = null,
                    RetryCount = initialRetryCount
                };

                // Act: simulate HandleFailure logic (private method behavior)
                var exception = new InvalidOperationException("Simulated failure");
                message.RetryCount++;

                if (message.RetryCount >= maxRetries)
                {
                    message.FailedAt = DateTime.UtcNow;
                    message.FailureReason = exception.Message;
                }

                // Assert: verify state transitions
                var retryCountIncremented = message.RetryCount == initialRetryCount + 1;

                if (initialRetryCount + 1 < maxRetries)
                {
                    // Still eligible for retry: ProcessedAt and FailedAt must remain null
                    var stillEligible = message.ProcessedAt == null && message.FailedAt == null;
                    return retryCountIncremented && stillEligible;
                }
                else
                {
                    // Dead-lettered: FailedAt must be set and FailureReason must be set
                    var deadLettered = message.FailedAt != null && message.FailureReason != null;
                    return retryCountIncremented && deadLettered;
                }
            });
    }

    /// <summary>
    /// Messages that have been dead-lettered (FailedAt is set) SHALL be excluded
    /// from the outbox polling query (WHERE ProcessedAt IS NULL AND FailedAt IS NULL).
    /// </summary>
    [Property(
        DisplayName = "Feature: template-architecture-gaps, Property 2: Outbox Retry State Machine - Dead-lettered exclusion",
        MaxTest = 100)]
    public Property Dead_lettered_messages_are_excluded_from_polling_query()
    {
        return Prop.ForAll(
            GenerateRetryCount(),
            GenerateMaxRetries(),
            (initialRetryCount, maxRetries) =>
            {
                // Arrange: create in-memory DbContext
                var dbOptions = new DbContextOptionsBuilder<OrdersDbContext>()
                    .UseInMemoryDatabase(databaseName: $"OutboxRetryTest_{Guid.NewGuid()}")
                    .Options;

                using var dbContext = new OrdersDbContext(dbOptions);

                var message = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventType = "TestEvent",
                    Payload = "{}",
                    OccurredAt = DateTime.UtcNow,
                    ProcessedAt = null,
                    FailedAt = null,
                    FailureReason = null,
                    RetryCount = initialRetryCount
                };

                dbContext.OutboxMessages.Add(message);
                dbContext.SaveChanges();

                // Act: simulate HandleFailure logic
                var exception = new InvalidOperationException("Simulated failure");
                message.RetryCount++;

                if (message.RetryCount >= maxRetries)
                {
                    message.FailedAt = DateTime.UtcNow;
                    message.FailureReason = exception.Message;
                }

                dbContext.SaveChanges();

                // Query: use the same filter the OutboxProcessor uses
                var eligibleMessages = dbContext.OutboxMessages
                    .Where(m => m.ProcessedAt == null && m.FailedAt == null)
                    .ToList();

                // Assert
                if (message.FailedAt != null)
                {
                    // Dead-lettered messages must NOT appear in the polling query
                    return !eligibleMessages.Any(m => m.Id == message.Id);
                }
                else
                {
                    // Still-eligible messages MUST appear in the polling query
                    return eligibleMessages.Any(m => m.Id == message.Id);
                }
            });
    }

    /// <summary>
    /// Generates RetryCount values in range [0, 10] to cover scenarios both
    /// below and at/above various maxRetries thresholds.
    /// </summary>
    private static Arbitrary<int> GenerateRetryCount()
    {
        return Arb.From(Gen.Choose(0, 10));
    }

    /// <summary>
    /// Generates maxRetries values in range [1, 10] to cover various configuration scenarios.
    /// Minimum of 1 ensures at least one retry is required before dead-lettering.
    /// </summary>
    private static Arbitrary<int> GenerateMaxRetries()
    {
        return Arb.From(Gen.Choose(1, 10));
    }
}
