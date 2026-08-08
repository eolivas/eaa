using Orders.Domain;
using Orders.Domain.Events;
using Orders.Domain.Exceptions;
using Xunit;

namespace Orders.Domain.Tests;

/// <summary>
/// Unit tests for the aggregate root.
/// Demonstrates: testing state transitions, domain events, and invariant enforcement.
/// Replace with your domain-specific aggregate tests.
/// </summary>
public class OrderTests
{
    private static OrderLine CreateLine(int quantity = 2, decimal unitPrice = 10.00m)
        => OrderLine.Create(ProductId.New(), quantity, new Money(unitPrice, "USD"));

    private static IReadOnlyList<OrderLine> CreateLines(int count = 1)
        => Enumerable.Range(1, count).Select(_ => CreateLine()).ToList();

    public class CreateMethod
    {
        [Fact]
        public void HappyPath_CreatesWithPendingStatus()
        {
            var customerId = CustomerId.New();
            var lines = CreateLines(2);

            var order = Order.Create(customerId, lines);

            Assert.Equal(OrderStatus.Pending, order.Status);
            Assert.Equal(customerId, order.CustomerId);
            Assert.Equal(2, order.Lines.Count);
            Assert.NotEqual(default, order.Id);
        }

        [Fact]
        public void HappyPath_RaisesCreatedEvent()
        {
            var customerId = CustomerId.New();
            var lines = CreateLines();

            var order = Order.Create(customerId, lines);

            var domainEvent = Assert.Single(order.DomainEvents);
            var createdEvent = Assert.IsType<OrderCreatedEvent>(domainEvent);
            Assert.Equal(order.Id, createdEvent.OrderId);
            Assert.Equal(customerId, createdEvent.CustomerId);
        }

        [Fact]
        public void WithNullLines_ThrowsDomainException()
        {
            Assert.Throws<OrderDomainException>(
                () => Order.Create(CustomerId.New(), null!));
        }

        [Fact]
        public void WithEmptyLines_ThrowsDomainException()
        {
            Assert.Throws<OrderDomainException>(
                () => Order.Create(CustomerId.New(), Array.Empty<OrderLine>()));
        }
    }

    public class PlaceMethod
    {
        [Fact]
        public void HappyPath_TransitionsToPlacedStatus()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());

            order.Place();

            Assert.Equal(OrderStatus.Placed, order.Status);
        }

        [Fact]
        public void HappyPath_RaisesPlacedEvent()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());
            order.ClearDomainEvents();

            order.Place();

            var domainEvent = Assert.Single(order.DomainEvents);
            Assert.IsType<OrderPlacedEvent>(domainEvent);
        }

        [Fact]
        public void OnAlreadyPlaced_ThrowsDomainException()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());
            order.Place();

            Assert.Throws<OrderDomainException>(() => order.Place());
        }
    }

    public class CancelMethod
    {
        [Fact]
        public void FromPending_TransitionsToCancelled()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());

            order.Cancel("Changed my mind");

            Assert.Equal(OrderStatus.Cancelled, order.Status);
        }

        [Fact]
        public void HappyPath_RaisesCancelledEvent()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());
            order.ClearDomainEvents();

            order.Cancel("Test reason");

            var domainEvent = Assert.Single(order.DomainEvents);
            var cancelledEvent = Assert.IsType<OrderCancelledEvent>(domainEvent);
            Assert.Equal("Test reason", cancelledEvent.Reason);
        }

        [Fact]
        public void OnCancelled_ThrowsDomainException()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());
            order.Cancel("First");

            Assert.Throws<OrderDomainException>(() => order.Cancel("Second"));
        }
    }
}
