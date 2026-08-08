---
inclusion: auto
---

# Testing Conventions

All tests live in `tests/` with one test project per source project. Follow these conventions.

## Test Project Structure

```
tests/
├── Orders.Domain.Tests/        → Unit tests + property tests for domain logic
├── Orders.Application.Tests/   → Unit tests for handlers (mocked deps)
├── Orders.Infrastructure.Tests/→ Outbox, messaging, and persistence tests (property + unit)
├── Orders.Architecture.Tests/  → Architecture enforcement (NetArchTest)
├── Orders.Api.Tests/           → Middleware & endpoint property tests (WebApplicationFactory)
├── Orders.Integration.Tests/   → End-to-end tests (WebApplicationFactory + Testcontainers)
└── Orders.Template.Tests/      → Template-specific validation
```

## Frameworks and Libraries

- **xUnit** — test framework
- **FluentAssertions** — assertion library (Application/Infrastructure tests)
- **Moq** — mocking (Application handler tests)
- **FsCheck.Xunit** — property-based testing (C# backend)
- **NetArchTest.Rules** — architecture enforcement
- **Bogus (Fakers)** — test data generation (e.g., `OrderFaker.cs`)
- **Testcontainers.PostgreSql** — real PostgreSQL for integration tests
- **Microsoft.AspNetCore.Mvc.Testing** — WebApplicationFactory for API tests
- **MassTransit.Testing** — InMemory test harness for message assertions

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
dotnet test --filter "FullyQualifiedName!~Integration"  # Exclude integration (needs Docker)
dotnet test --configuration Release --collect:"XPlat Code Coverage"  # With coverage
```

Coverage threshold: **80%** (enforced in CI).

## Property-Based Testing (FsCheck — C#)

Use FsCheck for testing correctness properties — universal statements that must hold for all valid inputs.

```csharp
using FsCheck;
using FsCheck.Xunit;

[Property(MaxTest = 100, DisplayName = "Feature: template-architecture-gaps, Property N: Title")]
public Property BatchRetrieval_RespectsMaxSize()
{
    return Prop.ForAll(
        Arb.Default.PositiveInt().Filter(n => n.Item <= 1000),
        batchSize =>
        {
            // Arrange: generate messages
            // Act: query with batch size
            // Assert: result.Count <= batchSize
            return (result.Count <= batchSize.Item).Label($"Got {result.Count} > {batchSize.Item}");
        });
}
```

Rules:
- Use `[Property]` attribute from `FsCheck.Xunit` (not `[Fact]`)
- Set `MaxTest = 100` minimum
- Include `DisplayName` with format: `"Feature: {feature-name}, Property N: {title}"`
- Return `Property` type, use `Prop.ForAll` with generators
- Use `.Label()` for descriptive failure messages
- Test against in-memory DbContext for persistence properties
- Test against `DefaultHttpContext` for middleware properties

### Frontend Property Tests (fast-check — TypeScript)

```typescript
import { fc } from '@fast-check/vitest';
import { describe, expect } from 'vitest';

// Feature: template-architecture-gaps, Property 13: ProblemDetails Field Error Display
describe('ProblemDetails field error display', () => {
  fc.test.prop([problemDetailsArbitrary])('renders errors for every field key', (problemDetails) => {
    // Arrange: render component with generated ProblemDetails
    // Assert: each field key has at least one visible error
  });
});
```

Rules:
- Use `fast-check` with Vitest
- 100 iterations minimum
- Comment tag: `// Feature: {feature-name}, Property N: {title}`
- Generate arbitrary valid/invalid inputs to prove universal properties

## Integration Testing (WebApplicationFactory + Testcontainers)

Integration tests exercise the full request pipeline with a real database:

```csharp
[Collection("Integration")]
public class PlaceOrderIntegrationTests : IntegrationTestBase
{
    public PlaceOrderIntegrationTests(OrdersWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task PlaceOrder_WithValidPayload_Returns201AndPersistsOrder()
    {
        var request = new { customerId = Guid.NewGuid(), lines = new[] { ... } };
        var response = await Client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
```

The `OrdersWebApplicationFactory`:
- Replaces PostgreSQL with Testcontainers PostgreSQL container
- Replaces MassTransit with InMemory test harness (for message assertions)
- Bypasses JWT authentication with a test handler that always succeeds
- Resets database between test classes

Rules:
- Integration tests live in `Orders.Integration.Tests`
- All test classes use `[Collection("Integration")]` to share the factory
- Inherit from `IntegrationTestBase` which provides `Client` and `Factory`
- These tests require Docker Desktop running locally
