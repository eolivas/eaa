using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Orders.Infrastructure.Http;

/// <summary>
/// Extension methods for registering HTTP client services in the DI container.
/// </summary>
public static class HttpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="InventoryHttpClient"/> typed HTTP client with standard resilience handling.
    /// The resilience handler provides automatic retry, circuit-breaker, and timeout out of the box.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration for reading base URLs.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInventoryHttpClient(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient<InventoryHttpClient>(client =>
        {
            client.BaseAddress = new Uri(configuration["Inventory:BaseUrl"] ?? "http://localhost:5100");
        })
        .AddStandardResilienceHandler();

        return services;
    }
}
