using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Extensions;

namespace Orders.Api.Tests.Cors;

/// <summary>
/// Property-based tests for CORS rejection of non-allowed origins.
/// Validates: Requirements 8.2
/// </summary>
public class CorsRejectionPropertyTests
{
    private static readonly string[] AllowedOrigins = new[]
    {
        "http://localhost:3000",
        "https://app.example.com"
    };

    /// <summary>
    /// For any HTTP request whose Origin header value is not present in the configured
    /// allowed origins list, the response SHALL NOT contain Access-Control-Allow-Origin,
    /// Access-Control-Allow-Methods, or Access-Control-Allow-Headers headers.
    /// </summary>
    [Property(
        DisplayName = "Feature: template-architecture-gaps, Property 5: CORS Rejection for Non-Allowed Origins",
        MaxTest = 100)]
    public Property Non_allowed_origins_do_not_receive_cors_headers()
    {
        return Prop.ForAll(
            GenerateNonAllowedOrigin(),
            origin =>
            {
                // Arrange: create a minimal web app with CORS configured
                using var host = CreateTestHost();
                var client = host.CreateClient();

                // Act: send a preflight OPTIONS request with the non-allowed origin
                var request = new HttpRequestMessage(HttpMethod.Options, "/test");
                request.Headers.Add("Origin", origin);
                request.Headers.Add("Access-Control-Request-Method", "GET");

                var response = client.SendAsync(request).GetAwaiter().GetResult();

                // Assert: response should NOT contain any CORS headers
                var hasAllowOrigin = response.Headers.Contains("Access-Control-Allow-Origin");
                var hasAllowMethods = response.Headers.Contains("Access-Control-Allow-Methods");
                var hasAllowHeaders = response.Headers.Contains("Access-Control-Allow-Headers");

                return (!hasAllowOrigin && !hasAllowMethods && !hasAllowHeaders)
                    .Label($"Origin: '{origin}' — " +
                           $"Allow-Origin present: {hasAllowOrigin}, " +
                           $"Allow-Methods present: {hasAllowMethods}, " +
                           $"Allow-Headers present: {hasAllowHeaders}");
            });
    }

    private static TestServer CreateTestHost()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = AllowedOrigins[0],
                ["Cors:AllowedOrigins:1"] = AllowedOrigins[1]
            })
            .Build();

        var builder = new WebHostBuilder()
            .UseConfiguration(configuration)
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(configuration);
                services.AddCorsPolicy(configuration);
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseCors();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/test", () => "OK");
                });
            });

        return new TestServer(builder);
    }

    private static Arbitrary<string> GenerateNonAllowedOrigin()
    {
        var gen = Gen.OneOf(
            // Random http:// origins with random subdomains/ports
            from subdomain in Gen.Elements("evil", "attacker", "unknown", "test", "hacker", "random")
            from domain in Gen.Elements("example.com", "malicious.net", "bad-actor.org", "phishing.io", "evil.co")
            from port in Gen.Choose(3001, 9999)
            select $"http://{subdomain}.{domain}:{port}",

            // Random https:// origins
            from subdomain in Gen.Elements("api", "app", "www", "admin", "portal", "dashboard")
            from domain in Gen.Elements("attacker.com", "notallowed.org", "badsite.net", "fakesite.io")
            select $"https://{subdomain}.{domain}",

            // Origins that look similar to allowed but are not exact matches
            Gen.Elements(
                "http://localhost:3001",
                "http://localhost:8080",
                "https://localhost:3000",
                "http://localhost",
                "http://app.example.com",
                "https://evil.example.com",
                "http://notlocalhost:3000",
                "https://app.example.com.evil.com"
            )
        );

        // Filter out any values that happen to be in the allowed list
        var filtered = gen.Where(origin =>
            !AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase));

        return Arb.From(filtered);
    }
}
