using Xunit;

namespace Orders.Integration.Tests;

/// <summary>
/// xUnit collection definition that shares a single OrdersWebApplicationFactory
/// (and its underlying Testcontainers PostgreSQL instance) across all test classes
/// in the "Integration" collection. Database is reset between test classes via
/// IntegrationTestBase.InitializeAsync().
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<OrdersWebApplicationFactory>
{
}
