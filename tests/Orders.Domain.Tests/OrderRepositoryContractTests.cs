using Orders.Domain;
using Xunit;

namespace Orders.Domain.Tests;

/// <summary>
/// Shared abstract contract tests for the repository interface.
/// Each implementation must extend this class and pass all tests,
/// enforcing the Liskov Substitution Principle across implementations.
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
        var repository = CreateRepository();
        var nonExistentId = OrderId.New();

        var result = await repository.GetByIdAsync(nonExistentId);

        Assert.Null(result);
    }
}
