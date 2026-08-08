using Orders.Domain;
using Orders.Domain.Common;
using Xunit;

namespace Orders.Domain.Tests;

/// <summary>
/// Tests for the base Entity identity-based equality.
/// These tests verify the DDD Entity pattern works correctly.
/// </summary>
public class EntityTests
{
    [Fact]
    public void Equals_SameId_ReturnsTrue()
    {
        var id = OrderId.New();
        var order1 = CreateOrderWithId(id);
        var order2 = CreateOrderWithId(id);

        Assert.True(order1.Equals(order2));
    }

    [Fact]
    public void Equals_DifferentId_ReturnsFalse()
    {
        var order1 = CreateOrderWithId(OrderId.New());
        var order2 = CreateOrderWithId(OrderId.New());

        Assert.False(order1.Equals(order2));
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        var order = CreateOrderWithId(OrderId.New());

        Assert.False(order.Equals((Entity<OrderId>?)null));
    }

    [Fact]
    public void GetHashCode_SameId_ReturnsSameHash()
    {
        var id = OrderId.New();
        var order1 = CreateOrderWithId(id);
        var order2 = CreateOrderWithId(id);

        Assert.Equal(order1.GetHashCode(), order2.GetHashCode());
    }

    [Fact]
    public void EqualityOperator_SameId_ReturnsTrue()
    {
        var id = OrderId.New();
        var order1 = CreateOrderWithId(id);
        var order2 = CreateOrderWithId(id);

        Assert.True(order1 == order2);
    }

    [Fact]
    public void InequalityOperator_DifferentId_ReturnsTrue()
    {
        var order1 = CreateOrderWithId(OrderId.New());
        var order2 = CreateOrderWithId(OrderId.New());

        Assert.True(order1 != order2);
    }

    private static Order CreateOrderWithId(OrderId id)
    {
        var customerId = CustomerId.New();
        var line = OrderLine.Create(ProductId.New(), 1, new Money(10m, "USD"));
        var order = Order.Create(customerId, new[] { line });
        var idProp = typeof(Entity<OrderId>).GetProperty(nameof(Order.Id))!;
        idProp.SetValue(order, id);
        return order;
    }
}
