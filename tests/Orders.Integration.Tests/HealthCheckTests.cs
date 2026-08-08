using System.Net;
using Xunit;

namespace Orders.Integration.Tests;

/// <summary>
/// Smoke tests validating that the integration test factory starts correctly
/// and the application responds to basic requests.
/// </summary>
[Collection("Integration")]
public class HealthCheckTests : IntegrationTestBase
{
    public HealthCheckTests(OrdersWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task LivenessEndpoint_ReturnsHealthy()
    {
        // Act
        var response = await Client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RootEndpoint_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Orders API", content);
    }
}
