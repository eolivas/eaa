namespace Orders.Domain;

/// <summary>
/// Strongly-typed identifier for a related entity (e.g., the owning user or parent).
/// Rename to match your domain concept.
/// </summary>
public readonly record struct CustomerId(Guid Value)
{
    public static CustomerId New() => new(Guid.NewGuid());
}
