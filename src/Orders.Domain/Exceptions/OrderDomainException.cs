namespace Orders.Domain.Exceptions;

/// <summary>
/// Exception thrown when a domain invariant is violated in the Orders bounded context.
/// Mapped to HTTP 422 Unprocessable Entity by the global exception-handling middleware.
/// </summary>
public class OrderDomainException : Exception
{
    public OrderDomainException(string message)
        : base(message)
    {
    }
}
