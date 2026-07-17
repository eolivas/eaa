using MediatR;
using Orders.Application.DTOs;

namespace Orders.Application.Queries;

/// <summary>
/// Query to retrieve a single order by its identifier.
/// </summary>
public record GetOrderQuery(Guid OrderId) : IRequest<OrderDto?>;
