using Orders.Domain;
using Orders.Domain.Events;
using Orders.Domain.Exceptions;
using Xunit;

namespace Orders.Domain.Tests;

public class OrderTests
{
    private static OrderLine CreateLine(int quantity = 2, decimal unitPrice = 10.00m)
        => OrderLine.Create(ProductId.New(), quantity, new Money(unitPrice, "USD"));

    private static IReadOnlyList<OrderLine> CreateLines(int count = 1)
        => Enumerable.Range(1, count).Select(_ => CreateLine()).ToList();

    public class CreateMethod
    {
        [Fact]
        public void HappyPath_CreatesOrderWithPendingStatus()
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
        public void HappyPath_RaisesOrderCreatedEvent()
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
        public void HappyPath_ComputesTotalFromLines()
        {
            var lines = new List<OrderLine>
            {
                OrderLine.Create(ProductId.New(), 3, new Money(5.00m, "USD")),
                OrderLine.Create(ProductId.New(), 2, new Money(10.00m, "USD"))
            };

            var order = Order.Create(CustomerId.New(), lines);

            Assert.Equal(new Money(35.00m, "USD"), order.Total);
        }

        [Fact]
        public void WithNullLines_ThrowsOrderDomainException()
        {
            var exception = Assert.Throws<OrderDomainException>(
                () => Order.Create(CustomerId.New(), null!));

            Assert.Contains("at least one line", exception.Message);
        }

        [Fact]
        public void WithEmptyLines_ThrowsOrderDomainException()
        {
            var exception = Assert.Throws<OrderDomainException>(
                () => Order.Create(CustomerId.New(), Array.Empty<OrderLine>()));

            Assert.Contains("at least one line", exception.Message);
        }
    }

    public class PlaceMethod
    {
        [Fact]
        public void HappyPath_TransitionsToPlaedStatus()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());

            order.Place();

            Assert.Equal(OrderStatus.Placed, order.Status);
        }

        [Fact]
        public void HappyPath_RaisesOrderPlacedEvent()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());
            order.ClearDomainEvents();

            order.Place();

            var domainEvent = Assert.Single(order.DomainEvents);
            var placedEvent = Assert.IsType<OrderPlacedEvent>(domainEvent);
            Assert.Equal(order.Id, placedEvent.OrderId);
        }

        [Fact]
        public void OnPlacedOrder_ThrowsOrderDomainException()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());
            order.Place();

            Assert.Throws<OrderDomainException>(() => order.Place());
        }

        [Fact]
        public void OnCancelledOrder_ThrowsOrderDomainException()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());
            order.Cancel("test");

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
        public void FromPlaced_TransitionsToCancelled()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());
            order.Place();

            order.Cancel("No longer needed");

            Assert.Equal(OrderStatus.Cancelled, order.Status);
        }

        [Fact]
        public void HappyPath_RaisesOrderCancelledEvent()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());
            order.ClearDomainEvents();

            order.Cancel("Test reason");

            var domainEvent = Assert.Single(order.DomainEvents);
            var cancelledEvent = Assert.IsType<OrderCancelledEvent>(domainEvent);
            Assert.Equal(order.Id, cancelledEvent.OrderId);
            Assert.Equal("Test reason", cancelledEvent.Reason);
        }

        [Fact]
        public void OnShippedOrder_ThrowsOrderDomainException()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());
            order.Place();

            // No public Ship() method yet; use reflection to simulate Shipped state.
            var statusProp = typeof(Order).GetProperty(nameof(Order.Status))!;
            statusProp.SetValue(order, OrderStatus.Shipped);

            Assert.Throws<OrderDomainException>(() => order.Cancel("Too late"));
        }

        [Fact]
        public void OnCancelledOrder_ThrowsOrderDomainException()
        {
            var order = Order.Create(CustomerId.New(), CreateLines());
            order.Cancel("First cancel");

            Assert.Throws<OrderDomainException>(() => order.Cancel("Second cancel"));
        }
    }
}
