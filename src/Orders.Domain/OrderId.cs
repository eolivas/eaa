namespace Orders.Domain;

/// <summary>
/// Strongly-typed identifier for the aggregate root.
/// </summary>
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
}
