namespace Orders.Domain.Pricing;

/// <summary>
/// Applies a seasonal discount (10% off) to the given price.
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
