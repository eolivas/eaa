using MediatR;

namespace Orders.Application.Commands;

/// <summary>
/// Command to cancel/delete an existing aggregate.
/// Replace with your domain-specific state transition command.
/// </summary>
public record CancelOrderCommand : IRequest<Unit>
{
    public Guid OrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
