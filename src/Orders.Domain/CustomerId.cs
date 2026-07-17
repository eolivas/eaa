namespace Orders.Domain;

/// <summary>
/// Strongly-typed identifier for a Customer.
/// </summary>
public readonly record struct CustomerId(Guid Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
}
