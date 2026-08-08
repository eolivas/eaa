namespace Orders.Domain.Pricing;

/// <summary>
/// Example discount strategy: applies a seasonal discount (10% off).
/// Replace with your domain-specific discount strategies.
/// </summary>
public sealed class SeasonalDiscountStrategy : IDiscountStrategy
{
    private const decimal DiscountRate = 0.10m;

    public Money Apply(Money price)
    {
        var discountedAmount = price.Amount * (1m - DiscountRate);
        return new Money(discountedAmount, price.Currency);
    }
}
