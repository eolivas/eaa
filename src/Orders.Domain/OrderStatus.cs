namespace Orders.Domain;

/// <summary>
/// Represents the lifecycle status of an Order.
/// Valid transitions: Pending → Placed, Placed → Shipped, Pending → Cancelled, Placed → Cancelled.
/// </summary>
public enum OrderStatus
{
    Pending,
    Placed,
    Shipped,
    Cancelled
}
