using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Extensions;

namespace Orders.Api.Tests.RateLimiting;

/// <summary>
/// Property-based tests for rate limit enforcement on /api/orders endpoints.
/// Validates: Requirements 9.1, 9.2
/// </summary>
public class RateLimitEnforcementPropertyTests
{
    /// <summary>
    /// For any configured permit limit N (1–10) and window of 60+ seconds,
    /// the first N requests succeed (HTTP 200), and the (N+1)th request returns
    /// HTTP 429 with a Retry-After header containing a positive integer.
    /// </summary>
    [Property(
        DisplayName = "Feature: template-architecture-gaps, Property 6: Rate Limit Enforcement",
        MaxTest = 100)]
    public Property Requests_exceeding_permit_limit_receive_429_with_retry_after()
    {
        return Prop.ForAll(
            GeneratePermitLimit(),
            permitLimit =>
            {
                const int windowSeconds = 120;

                using var server = CreateTestServer(permitLimit, windowSeconds);
                var client = server.CreateClient();

                // Send permitLimit requests — all should succeed
                for (int i = 0; i < permitLimit; i++)
                {
                    var successResponse = client.GetAsync("/api/orders").GetAwaiter().GetResult();
                    if ((int)successResponse.StatusCode != 200)
                    {
                        return false
                            .Label($"Request {i + 1}/{permitLimit} returned {(int)successResponse.StatusCode}, expected 200");
                    }
                }

                // Send one more request — should be rate limited
                var rejectedResponse = client.GetAsync("/api/orders").GetAwaiter().GetResult();
                var statusCode = (int)rejectedResponse.StatusCode;

                if (statusCode != 429)
                {
                    return false
                        .Label($"Request {permitLimit + 1} returned {statusCode}, expected 429 (permitLimit={permitLimit})");
                }

                // Verify Retry-After header is present and contains a positive integer
                var hasRetryAfter = rejectedResponse.Headers.Contains("Retry-After");
                if (!hasRetryAfter)
                {
                    return false
                        .Label($"429 response missing Retry-After header (permitLimit={permitLimit})");
                }

                var retryAfterValue = rejectedResponse.Headers.GetValues("Retry-After").First();
                var isPositiveInt = int.TryParse(retryAfterValue, out var retryAfterSeconds)
                                    && retryAfterSeconds > 0;

                return isPositiveInt
                    .Label($"Retry-After='{retryAfterValue}', expected positive integer (permitLimit={permitLimit})");
            });
    }

    private static TestServer CreateTestServer(int permitLimit, int windowSeconds)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:PermitLimit"] = permitLimit.ToString(),
                ["RateLimit:WindowSeconds"] = windowSeconds.ToString()
            })
            .Build();

        var builder = new WebHostBuilder()
            .UseConfiguration(configuration)
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(configuration);
                services.AddRouting();
                services.AddOrdersRateLimiter(configuration);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseRateLimiter();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/api/orders", () => Results.Ok())
                        .RequireRateLimiting(RateLimitingServiceCollectionExtensions.PolicyName);
                });
            });

        return new TestServer(builder);
    }

    /// <summary>
    /// Generates permit limits between 1 and 10 (small values for fast testing).
    /// </summary>
    private static Arbitrary<int> GeneratePermitLimit()
    {
        var gen = Gen.Choose(1, 10);
        return Arb.From(gen);
    }
}
