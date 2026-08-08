using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Orders.Api.Extensions;

/// <summary>
/// Extension methods for configuring CORS in the DI container.
/// Reads allowed origins from the Cors:AllowedOrigins configuration section.
/// </summary>
public static class CorsServiceCollectionExtensions
{
    /// <summary>
    /// Registers CORS services with a default policy that allows configured origins,
    /// specific HTTP methods and headers, credentials, and a preflight max age of 600 seconds.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration for reading allowed origins.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? Array.Empty<string>();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(allowedOrigins)
                      .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                      .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id")
                      .AllowCredentials()
                      .SetPreflightMaxAge(TimeSpan.FromSeconds(600));
            });
        });

        return services;
    }
}
