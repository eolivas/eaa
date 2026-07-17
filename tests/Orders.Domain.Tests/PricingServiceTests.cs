using Orders.Domain;
using Orders.Domain.Pricing;
using Xunit;

namespace Orders.Domain.Tests;

public class PricingServiceTests
{
    [Fact]
    public void Calculate_WithNoStrategies_ReturnsSamePrice()
    {
        var service = new PricingService(Array.Empty<IDiscountStrategy>());
        var basePrice = new Money(100m, "USD");

        var result = service.Calculate(basePrice);

        Assert.Equal(basePrice, result);
    }

    [Fact]
    public void Calculate_WithSeasonalDiscount_Applies10PercentOff()
    {
        var service = new PricingService(new[] { new SeasonalDiscountStrategy() });
        var basePrice = new Money(100m, "USD");

        var result = service.Calculate(basePrice);

        Assert.Equal(90m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Calculate_WithLoyaltyDiscount_Applies5PercentOff()
    {
        var service = new PricingService(new[] { new LoyaltyDiscountStrategy() });
        var basePrice = new Money(100m, "USD");

        var result = service.Calculate(basePrice);

        Assert.Equal(95m, result.Amount);
    }

    [Fact]
    public void Calculate_WithBothStrategies_AppliesSequentially()
    {
        var strategies = new IDiscountStrategy[]
        {
            new SeasonalDiscountStrategy(),
            new LoyaltyDiscountStrategy()
        };
        var service = new PricingService(strategies);
        var basePrice = new Money(100m, "USD");

        var result = service.Calculate(basePrice);

        // 100 * 0.90 = 90, 90 * 0.95 = 85.5
        Assert.Equal(85.50m, result.Amount);
    }

    [Fact]
    public void Calculate_ResultIsAlwaysLessThanOrEqualToBasePrice()
    {
        var strategies = new IDiscountStrategy[]
        {
            new SeasonalDiscountStrategy(),
            new LoyaltyDiscountStrategy()
        };
        var service = new PricingService(strategies);
        var basePrice = new Money(200m, "EUR");

        var result = service.Calculate(basePrice);

        Assert.True(result.Amount <= basePrice.Amount);
    }

    [Fact]
    public void Constructor_WithNullStrategies_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PricingService(null!));
    }

    [Fact]
    public void Calculate_WithNullBasePrice_ThrowsArgumentNullException()
    {
        var service = new PricingService(Array.Empty<IDiscountStrategy>());

        Assert.Throws<ArgumentNullException>(() => service.Calculate(null!));
    }
}
