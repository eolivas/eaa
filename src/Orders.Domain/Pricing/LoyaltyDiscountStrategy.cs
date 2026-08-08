namespace Orders.Domain.Pricing;

/// <summary>
/// Example discount strategy: applies a loyalty discount (5% off).
/// Replace with your domain-specific discount strategies.
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
