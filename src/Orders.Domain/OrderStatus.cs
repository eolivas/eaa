namespace Orders.Domain;

/// <summary>
/// Represents the lifecycle status of the aggregate.
/// Replace with your domain-specific states.
/// </summary>
public enum OrderStatus
{
    Pending,
    Placed,
    Shipped,
    Cancelled
}
