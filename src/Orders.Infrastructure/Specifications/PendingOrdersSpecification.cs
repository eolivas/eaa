using System.Linq.Expressions;
using Orders.Domain;

namespace Orders.Infrastructure.Specifications;

/// <summary>
/// Specification that matches Orders with Status == OrderStatus.Pending.
/// </summary>
public sealed class PendingOrdersSpecification : Specification<Order>
{
    public override Expression<Func<Order, bool>> ToExpression()
    {
        return order => order.Status == OrderStatus.Pending;
    }
}
