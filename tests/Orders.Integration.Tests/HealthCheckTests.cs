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
        var response = await Client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RootEndpoint_ReturnsOk()
    {
        var response = await Client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
