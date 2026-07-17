using Orders.Domain;
using Xunit;

namespace Orders.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Constructor_WithNegativeAmount_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Money(-1m, "USD"));
    }

    [Fact]
    public void Constructor_WithInvalidCurrency_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Money(10m, "usd"));
        Assert.Throws<ArgumentException>(() => new Money(10m, "US"));
        Assert.Throws<ArgumentException>(() => new Money(10m, "USDD"));
        Assert.Throws<ArgumentException>(() => new Money(10m, "12A"));
    }

    [Fact]
    public void Constructor_WithValidArgs_CreatesMoney()
    {
        var money = new Money(50.25m, "EUR");

        Assert.Equal(50.25m, money.Amount);
        Assert.Equal("EUR", money.Currency);
    }

    [Fact]
    public void Zero_ReturnsMoneyWithZeroAmount()
    {
        var zero = Money.Zero("GBP");

        Assert.Equal(0m, zero.Amount);
        Assert.Equal("GBP", zero.Currency);
    }

    [Fact]
    public void Addition_SameCurrency_AddAmounts()
    {
        var a = new Money(10m, "USD");
        var b = new Money(20m, "USD");

        var result = a + b;

        Assert.Equal(30m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Addition_DifferentCurrencies_ThrowsInvalidOperationException()
    {
        var a = new Money(10m, "USD");
        var b = new Money(20m, "EUR");

        Assert.Throws<InvalidOperationException>(() => a + b);
    }

    [Fact]
    public void MultiplyByInt_ReturnsScaledAmount()
    {
        var money = new Money(10m, "USD");

        var result = money * 3;

        Assert.Equal(30m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void MultiplyByDecimal_ReturnsScaledAmount()
    {
        var money = new Money(100m, "USD");

        var result = money * 0.5m;

        Assert.Equal(50m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var money = new Money(42.50m, "EUR");

        Assert.Equal("42.50 EUR", money.ToString());
    }
}
