---
inclusion: auto
---

# Testing Conventions

All tests live in `tests/` with one test project per source project. Follow these conventions.

## Test Project Structure

```
tests/
├── Orders.Domain.Tests/        → Unit tests for domain entities and value objects
├── Orders.Application.Tests/   → Unit tests for handlers (mocked deps)
├── Orders.Infrastructure.Tests/→ Integration tests for repositories
├── Orders.Architecture.Tests/  → Architecture enforcement (NetArchTest)
├── Orders.Api.Tests/           → Endpoint integration tests
└── Orders.Template.Tests/      → Template-specific validation
```

## Frameworks and Libraries

- **xUnit** — test framework
- **FluentAssertions** — assertion library (Application/Infrastructure tests)
- **Moq** — mocking (Application handler tests)
- **NetArchTest.Rules** — architecture enforcement
- **Bogus (Fakers)** — test data generation (e.g., `OrderFaker.cs`)

## Test Class Organization

Use **nested classes per method** under a top-level class per system-under-test:

```csharp
public class OrderTests
{
    // Shared helper methods at the top
    private static OrderLine CreateLine(int quantity = 2, decimal unitPrice = 10.00m)
        => OrderLine.Create(ProductId.New(), quantity, new Money(unitPrice, "USD"));

    public class CreateMethod
    {
        [Fact]
        public void HappyPath_CreatesOrderWithPendingStatus() { ... }

        [Fact]
        public void WithNullLines_ThrowsOrderDomainException() { ... }
    }

    public class PlaceMethod
    {
        [Fact]
        public void HappyPath_TransitionsToPlacedStatus() { ... }

        [Fact]
        public void OnCancelledOrder_ThrowsOrderDomainException() { ... }
    }
}
```

## Test Naming Pattern

```
{Scenario}_{ExpectedBehavior}
```

Examples:
- `HappyPath_CreatesOrderWithPendingStatus`
- `HappyPath_RaisesOrderPlacedEvent`
- `WithEmptyLines_ThrowsOrderDomainException`
- `OnShippedOrder_ThrowsOrderDomainException`
- `Handle_ValidCommand_CallsSaveAsyncOnce`
- `Handle_EmptyLines_ValidationBehaviourThrowsValidationException`

Rules:
- Start with `HappyPath_` for the primary success scenario
- Use `With{Condition}_` for specific input variations
- Use `On{State}_` for state-dependent behavior
- End with the expected outcome (verb + detail)

## Domain Tests Pattern

```csharp
[Fact]
public void HappyPath_ComputesTotalFromLines()
{
    var lines = new List<OrderLine>
    {
        OrderLine.Create(ProductId.New(), 3, new Money(5.00m, "USD")),
        OrderLine.Create(ProductId.New(), 2, new Money(10.00m, "USD"))
    };

    var order = Order.Create(CustomerId.New(), lines);

    Assert.Equal(new Money(35.00m, "USD"), order.Total);
}
```

- Use `Assert.*` from xUnit directly for domain tests
- No mocks — domain tests exercise pure logic
- Use static helper methods for test data creation

## Application Handler Tests Pattern

```csharp
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
    public async Task Handle_ValidCommand_CallsSaveAsyncOnce()
    {
        var command = new PlaceOrderCommand { /* ... */ };

        var result = await _handler.Handle(command, CancellationToken.None);

        _repoMock.Verify(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Once());
    }
}
```

- Mock all dependencies
- Verify interactions with `Times.Once()`, `Times.Never()`, `Times.AtLeastOnce()`
- Use FluentAssertions for complex assertions: `await act.Should().ThrowAsync<ValidationException>()`

## Architecture Tests Pattern

```csharp
[Fact]
public void Domain_should_not_depend_on_Infrastructure()
{
    var result = Types.InAssembly(DomainAssembly)
        .ShouldNot()
        .HaveDependencyOn("Orders.Infrastructure")
        .GetResult();

    Assert.True(result.IsSuccessful, "Domain layer must not depend on Infrastructure layer.");
}
```

- One test per forbidden dependency
- Use `NetArchTest.Rules`
- Assert with a descriptive failure message

## Test Data (Fakers)

Use Bogus for complex test data generation:

```csharp
// OrderFaker.cs in Orders.Domain.Tests
public class OrderFaker
{
    public static Order CreateValid(int lineCount = 1) { /* ... */ }
}
```

## Running Tests

```bash
dotnet test                                    # All tests
dotnet test --filter "FullyQualifiedName~Domain"  # Domain tests only
dotnet test --configuration Release --collect:"XPlat Code Coverage"  # With coverage
```

Coverage threshold: **80%** (enforced in CI).
