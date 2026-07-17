namespace Orders.Domain.Pricing;

/// <summary>
/// Defines a discount strategy that can be applied to a price.
/// New strategies can be added without modifying <see cref="PricingService"/>,
/// satisfying the Open/Closed Principle.
/// </summary>
public interface IDiscountStrategy
{
    /// <summary>
    /// Applies the discount to the given price and returns the discounted price.
    /// The returned value must always be ≤ the input price.
    /// </summary>
    Money Apply(Money price);
}
