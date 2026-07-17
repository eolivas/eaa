namespace Orders.Domain;

/// <summary>
/// Strongly-typed identifier for an OrderLine entity.
/// </summary>
public readonly record struct OrderLineId(Guid Value)
{
    public static OrderLineId New() => new(Guid.NewGuid());
}
