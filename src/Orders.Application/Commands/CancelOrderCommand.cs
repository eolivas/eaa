using MediatR;

namespace Orders.Application.Commands;

/// <summary>
/// Command to cancel an existing order with a given reason.
/// </summary>
public record CancelOrderCommand : IRequest<Unit>
{
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
