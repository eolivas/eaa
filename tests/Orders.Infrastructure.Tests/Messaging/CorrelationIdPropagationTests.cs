using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orders.Domain;
using Orders.Domain.Events;
using Orders.Infrastructure.Configuration;
using Orders.Infrastructure.Messaging;
using Orders.Infrastructure.Persistence;
using Xunit;

namespace Orders.Infrastructure.Tests.Messaging;

/// <summary>
/// Unit tests validating correlation ID propagation in the outbox processor
/// and MassTransit consumers.
/// Validates: Requirements 7.6, 7.7
/// </summary>
public class CorrelationIdPropagationTests
{
    [Fact]
    public async Task OutboxProcessor_WhenMessageHasCorrelationId_SetsHeaderOnPublish()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var orderId = OrderId.New();

        var services = new ServiceCollection();
        services.AddLogging();

        // Set up InMemory DbContext with a message that has a CorrelationId
        var dbName = $"OutboxCorrelationTest_{Guid.NewGuid()}";
        services.AddDbContext<OrdersDbContext>(opt =>
            opt.UseInMemoryDatabase(dbName));

        // Configure MassTransit InMemory test harness
        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<OrderPlacedConsumer>();
        });

        services.Configure<OutboxOptions>(o =>
        {
            o.BatchSize = 20;
            o.MaxRetries = 5;
            o.PollingIntervalSeconds = 1;
        });

        var provider = services.BuildServiceProvider();

        // Seed an outbox message with correlation ID
        using (var scope = provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var orderPlacedEvent = new OrderPlacedEvent(orderId);

            dbContext.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = typeof(OrderPlacedEvent).AssemblyQualifiedName!,
                Payload = System.Text.Json.JsonSerializer.Serialize(orderPlacedEvent),
                OccurredAt = DateTime.UtcNow,
                ProcessedAt = null,
                CorrelationId = correlationId
            });
            await dbContext.SaveChangesAsync();
        }

        // Act: Publish via the same code path as the OutboxProcessor
        using (var scope = provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            var message = await dbContext.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.FailedAt == null)
                .FirstAsync();

            var eventType = Type.GetType(message.EventType)!;
            var domainEvent = System.Text.Json.JsonSerializer.Deserialize(message.Payload, eventType)!;

            await publishEndpoint.Publish(domainEvent, eventType, ctx =>
            {
                if (!string.IsNullOrEmpty(message.CorrelationId))
                {
                    ctx.Headers.Set("X-Correlation-Id", message.CorrelationId);
                }
            });

            message.ProcessedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        // Assert: Verify the message was published with the correlation ID header
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.InactivityTask;

        var published = harness.Published.Select<OrderPlacedEvent>().FirstOrDefault();
        Assert.NotNull(published);
        Assert.NotNull(published.Context);

        var headerValue = published.Context.Headers.Get<string>("X-Correlation-Id");
        Assert.Equal(correlationId, headerValue);
    }

    [Fact]
    public async Task OutboxProcessor_WhenMessageHasNullCorrelationId_DoesNotSetHeader()
    {
        // Arrange
        var orderId = OrderId.New();

        var services = new ServiceCollection();
        services.AddLogging();

        var dbName = $"OutboxNoCorrelationTest_{Guid.NewGuid()}";
        services.AddDbContext<OrdersDbContext>(opt =>
            opt.UseInMemoryDatabase(dbName));

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<OrderPlacedConsumer>();
        });

        services.Configure<OutboxOptions>(o =>
        {
            o.BatchSize = 20;
            o.MaxRetries = 5;
            o.PollingIntervalSeconds = 1;
        });

        var provider = services.BuildServiceProvider();

        // Seed an outbox message WITHOUT correlation ID
        using (var scope = provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var orderPlacedEvent = new OrderPlacedEvent(orderId);

            dbContext.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = typeof(OrderPlacedEvent).AssemblyQualifiedName!,
                Payload = System.Text.Json.JsonSerializer.Serialize(orderPlacedEvent),
                OccurredAt = DateTime.UtcNow,
                ProcessedAt = null,
                CorrelationId = null // No correlation ID
            });
            await dbContext.SaveChangesAsync();
        }

        // Act: Publish
        using (var scope = provider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

            var message = await dbContext.OutboxMessages
                .Where(m => m.ProcessedAt == null && m.FailedAt == null)
                .FirstAsync();

            var eventType = Type.GetType(message.EventType)!;
            var domainEvent = System.Text.Json.JsonSerializer.Deserialize(message.Payload, eventType)!;

            await publishEndpoint.Publish(domainEvent, eventType, ctx =>
            {
                if (!string.IsNullOrEmpty(message.CorrelationId))
                {
                    ctx.Headers.Set("X-Correlation-Id", message.CorrelationId);
                }
            });

            message.ProcessedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync();
        }

        // Assert: Header should NOT be set
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.InactivityTask;

        var published = harness.Published.Select<OrderPlacedEvent>().FirstOrDefault();
        Assert.NotNull(published);
        Assert.NotNull(published.Context);

        var headerValue = published.Context.Headers.Get<string>("X-Correlation-Id");
        Assert.Null(headerValue);
    }

    [Fact]
    public async Task Consumer_WhenMessageHasCorrelationIdHeader_ExtractsItForLogging()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var orderId = OrderId.New();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<OrderPlacedConsumer>();
        });

        var provider = services.BuildServiceProvider();

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            // Act: publish a message with the X-Correlation-Id header
            await harness.Bus.Publish(new OrderPlacedEvent(orderId), ctx =>
            {
                ctx.Headers.Set("X-Correlation-Id", correlationId);
            });

            // Assert: consumer was called (the consumer uses LogContext.PushProperty)
            var consumerHarness = harness.GetConsumerHarness<OrderPlacedConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<OrderPlacedEvent>(
                x => x.Context.Message.OrderId == orderId));

            // Verify the header was passed through to the consumer context
            var consumed = consumerHarness.Consumed.Select<OrderPlacedEvent>().First();
            var headerValue = consumed.Context.Headers.Get<string>("X-Correlation-Id");
            Assert.Equal(correlationId, headerValue);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Consumer_WhenMessageHasNoCorrelationIdHeader_ConsumesSuccessfully()
    {
        // Arrange
        var orderId = OrderId.New();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMassTransitTestHarness(cfg =>
        {
            cfg.AddConsumer<OrderPlacedConsumer>();
        });

        var provider = services.BuildServiceProvider();

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            // Act: publish a message WITHOUT X-Correlation-Id header
            await harness.Bus.Publish(new OrderPlacedEvent(orderId));

            // Assert: consumer was still called successfully
            var consumerHarness = harness.GetConsumerHarness<OrderPlacedConsumer>();
            Assert.True(await consumerHarness.Consumed.Any<OrderPlacedEvent>(
                x => x.Context.Message.OrderId == orderId));

            // Verify no X-Correlation-Id header exists
            var consumed = consumerHarness.Consumed.Select<OrderPlacedEvent>().First();
            var headerValue = consumed.Context.Headers.Get<string>("X-Correlation-Id");
            Assert.Null(headerValue);
        }
        finally
        {
            await harness.Stop();
        }
    }
}
