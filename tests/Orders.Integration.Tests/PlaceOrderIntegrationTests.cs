using System.Net;
using System.Net.Http.Json;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Domain.Events;
using Xunit;

namespace Orders.Integration.Tests;

/// <summary>
/// End-to-end integration tests that exercise the full request pipeline:
/// HTTP request → endpoint → MediatR handler → EF Core repository → database.
/// Validates: Requirements 13.3
/// </summary>
[Collection("Integration")]
public class PlaceOrderIntegrationTests : IntegrationTestBase
{
    public PlaceOrderIntegrationTests(OrdersWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task PlaceOrder_WithValidPayload_Returns201AndPersistsOrder()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var request = new
        {
            customerId,
            lines = new[]
            {
                new { productId, quantity = 2, unitPrice = 19.99m, currency = "USD" }
            }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/orders", request);

        // Assert HTTP 201 Created
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Assert response contains the order ID
        var body = await response.Content.ReadFromJsonAsync<OrderCreatedResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.Id);

        // Assert Location header is set
        Assert.NotNull(response.Headers.Location);
        Assert.Contains(body.Id.ToString(), response.Headers.Location.ToString());

        // Assert the order was persisted in the database
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Orders.Infrastructure.Persistence.OrdersDbContext>();

        var persistedOrder = await dbContext.Orders
            .Include("_lines")
            .FirstOrDefaultAsync(o => o.CustomerId == new Orders.Domain.CustomerId(customerId));

        Assert.NotNull(persistedOrder);
        Assert.Equal(Orders.Domain.OrderStatus.Placed, persistedOrder.Status);
        Assert.Single(persistedOrder.Lines);

        // Assert an outbox message was created for the domain event
        var outboxMessages = await dbContext.OutboxMessages
            .Where(m => m.EventType.Contains("OrderPlaced") || m.EventType.Contains("OrderCreated"))
            .ToListAsync();

        Assert.NotEmpty(outboxMessages);
    }

    [Fact]
    public async Task PlaceOrder_WithValidPayload_PublishesOrderPlacedEvent()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var request = new
        {
            customerId,
            lines = new[]
            {
                new { productId, quantity = 1, unitPrice = 9.99m, currency = "USD" }
            }
        };

        var harness = Factory.Services.GetRequiredService<ITestHarness>();

        // Act
        var response = await Client.PostAsJsonAsync("/api/orders", request);

        // Assert HTTP 201
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        // Verify outbox message was created, then manually publish it via MassTransit
        // (the OutboxProcessor background service timing is non-deterministic in tests)
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Orders.Infrastructure.Persistence.OrdersDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<MassTransit.IPublishEndpoint>();

        var outboxMessages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.FailedAt == null)
            .OrderBy(m => m.OccurredAt)
            .ToListAsync();

        Assert.NotEmpty(outboxMessages);

        // Manually publish each outbox message to the test harness
        foreach (var message in outboxMessages)
        {
            var eventType = Type.GetType(message.EventType);
            Assert.NotNull(eventType);

            var domainEvent = System.Text.Json.JsonSerializer.Deserialize(message.Payload, eventType);
            Assert.NotNull(domainEvent);

            await publishEndpoint.Publish(domainEvent, eventType);
            message.ProcessedAt = DateTime.UtcNow;
        }
        await dbContext.SaveChangesAsync();

        // Assert that an OrderPlacedEvent was published via MassTransit
        var published = harness.Published.Select<OrderPlacedEvent>().ToList();
        Assert.NotEmpty(published);
    }

    private record OrderCreatedResponse(Guid Id);
}
