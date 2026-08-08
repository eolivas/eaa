using Microsoft.Extensions.DependencyInjection;
using Orders.Infrastructure.Persistence;
using Xunit;

namespace Orders.Integration.Tests;

/// <summary>
/// Base class for integration tests providing access to the factory,
/// a pre-configured HttpClient, and database reset between test classes.
/// Implements IClassFixture for shared factory and IAsyncLifetime for per-class reset.
/// </summary>
[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected OrdersWebApplicationFactory Factory { get; }
    protected HttpClient Client { get; }

    protected IntegrationTestBase(OrdersWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>
    /// Resets the database before each test class runs, ensuring full isolation.
    /// </summary>
    public async Task InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a scoped service from the application's DI container.
    /// </summary>
    protected T GetService<T>() where T : notnull
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets the OrdersDbContext for direct database assertions.
    /// </summary>
    protected OrdersDbContext GetDbContext()
    {
        return GetService<OrdersDbContext>();
    }
}
