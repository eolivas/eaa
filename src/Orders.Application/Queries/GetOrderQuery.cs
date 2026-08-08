using MediatR;
using Orders.Application.DTOs;

namespace Orders.Application.Queries;

/// <summary>
/// Query to retrieve a single aggregate by its identifier.
/// Demonstrates: CQRS query separation.
/// </summary>
public record GetOrderQuery(Guid OrderId) : IRequest<OrderDto?>;
