using MassTransit;
using Orders.Application.Interfaces;
using Orders.Domain.Common;

namespace Orders.Infrastructure.Messaging;

/// <summary>
/// Publishes domain events via MassTransit's publish endpoint.
/// </summary>
public sealed class MassTransitEventPublisher : IApplicationEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
    }

    public Task PublishAsync(DomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        return _publishEndpoint.Publish(domainEvent, domainEvent.GetType(), cancellationToken);
    }
}
