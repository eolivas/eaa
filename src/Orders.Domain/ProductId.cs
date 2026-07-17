namespace Orders.Domain;

/// <summary>
/// Strongly-typed identifier for a Product.
/// </summary>
public readonly record struct ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.NewGuid());
}
