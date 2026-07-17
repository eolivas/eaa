using Orders.Domain.Common;

namespace Orders.Application.Interfaces;

/// <summary>
/// Abstraction for publishing domain events from the Application layer.
/// Concrete implementations (e.g., MassTransitEventPublisher) reside in Infrastructure,
/// satisfying the Dependency Inversion Principle (DIP).
/// </summary>
public interface IApplicationEventPublisher
{
    Task PublishAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default);
}
