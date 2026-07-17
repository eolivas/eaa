using System.Linq.Expressions;

namespace Orders.Infrastructure.Specifications;

/// <summary>
/// Abstract base class for the Specification Pattern.
/// Encapsulates a composable business-rule predicate as an Expression.
/// </summary>
public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();

    public bool IsSatisfiedBy(T entity)
    {
        return ToExpression().Compile()(entity);
    }
}
