namespace Orders.Domain;

/// <summary>
/// Strongly-typed identifier for a referenced entity.
/// Rename to match your domain concept.
/// </summary>
public readonly record struct ProductId(Guid Value)
{
    public static ProductId New() => new(Guid.NewGuid());
}
