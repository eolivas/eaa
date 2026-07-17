using FluentAssertions;
using FluentValidation;
using MediatR;
using Moq;
using Orders.Application.Behaviours;
using Orders.Application.Commands;
using Orders.Application.Interfaces;
using Orders.Domain;
using Orders.Domain.Common;
using Xunit;

namespace Orders.Application.Tests;

public class PlaceOrderHandlerTests
{
    private readonly Mock<IOrderRepository> _repoMock;
    private readonly Mock<IApplicationEventPublisher> _publisherMock;
    private readonly PlaceOrderHandler _handler;

    public PlaceOrderHandlerTests()
    {
        _repoMock = new Mock<IOrderRepository>();
        _publisherMock = new Mock<IApplicationEventPublisher>();
        _handler = new PlaceOrderHandler(_repoMock.Object, _publisherMock.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CallsSaveAsyncOnceAndPublishAsyncAtLeastOnce()
    {
        // Arrange
        var command = new PlaceOrderCommand
        {
            CustomerId = Guid.NewGuid(),
            Lines = new List<OrderLineDto>
            {
                new OrderLineDto
                {
                    ProductId = Guid.NewGuid(),
                    Quantity = 2,
                    UnitPrice = 9.99m,
                    Currency = "USD"
                }
            }
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repoMock.Verify(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once());
        _publisherMock.Verify(p => p.PublishAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task Handle_EmptyLines_ValidationBehaviourThrowsValidationException_SaveAsyncNeverCalled()
    {
        // Arrange
        var command = new PlaceOrderCommand
        {
            CustomerId = Guid.NewGuid(),
            Lines = new List<OrderLineDto>()
        };

        var validator = new PlaceOrderCommandValidator();
        var validators = new List<IValidator<PlaceOrderCommand>> { validator };

        var validationBehaviour = new ValidationBehaviour<PlaceOrderCommand, OrderId>(validators);

        RequestHandlerDelegate<OrderId> nextDelegate = () => _handler.Handle(command, CancellationToken.None);

        // Act
        Func<Task> act = () => validationBehaviour.Handle(command, nextDelegate, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _repoMock.Verify(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
