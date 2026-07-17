namespace Orders.Domain.Pricing;

/// <summary>
/// Applies a loyalty discount (5% off) to the given price.
/// </summary>
public sealed class LoyaltyDiscountStrategy : IDiscountStrategy
{
    private const decimal DiscountRate = 0.05m;

    public Money Apply(Money price)
    {
        var discountedAmount = price.Amount * (1m - DiscountRate);
        return new Money(discountedAmount, price.Currency);
    }
}
