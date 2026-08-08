namespace Orders.Domain.Pricing;

/// <summary>
/// Applies a pipeline of <see cref="IDiscountStrategy"/> instances sequentially
/// to compute a final discounted price. Open for extension (register new strategies)
/// without modifying this class (Open/Closed Principle).
/// </summary>
public sealed class PricingService
{
    private readonly IReadOnlyList<IDiscountStrategy> _strategies;

    public PricingService(IEnumerable<IDiscountStrategy> strategies)
    {
        _strategies = strategies?.ToList() ?? throw new ArgumentNullException(nameof(strategies));
    }

    /// <summary>
    /// Applies all registered discount strategies sequentially via Aggregate.
    /// </summary>
    public Money Calculate(Money basePrice)
    {
        ArgumentNullException.ThrowIfNull(basePrice);

        var result = _strategies.Aggregate(basePrice, (current, strategy) => strategy.Apply(current));

        return result.Amount > basePrice.Amount
            ? basePrice
            : result;
    }
}
