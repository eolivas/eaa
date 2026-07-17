using Orders.Domain;
using Xunit;

namespace Orders.Domain.Tests;

/// <summary>
/// Shared abstract contract tests for <see cref="IOrderRepository"/>.
/// Each implementation must extend this class and pass all tests,
/// enforcing the Liskov Substitution Principle across repository implementations.
/// </summary>
public abstract class OrderRepositoryContractTests<TImpl> where TImpl : IOrderRepository
{
    /// <summary>
    /// Creates a fresh instance of the repository implementation under test.
    /// </summary>
    protected abstract TImpl CreateRepository();

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ReturnsNull()
    {
        // Arrange
        var repository = CreateRepository();
        var nonExistentId = OrderId.New();

        // Act
        var result = await repository.GetByIdAsync(nonExistentId);

        // Assert
        Assert.Null(result);
    }
}
