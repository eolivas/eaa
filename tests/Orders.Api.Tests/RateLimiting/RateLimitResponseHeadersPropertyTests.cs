using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Extensions;
using Orders.Api.Middleware;

namespace Orders.Api.Tests.RateLimiting;

/// <summary>
/// Property-based tests for rate limit response headers.
/// Validates: Requirements 9.5
/// </summary>
public class RateLimitResponseHeadersPropertyTests
{
    /// <summary>
    /// For any request sequence within the rate limit, the response SHALL include
    /// X-RateLimit-Limit equal to the configured permit limit and X-RateLimit-Remaining
    /// equal to permitLimit - requestCount (number of requests made so far inclusive).
    /// </summary>
    [Property(
        DisplayName = "Feature: template-architecture-gaps, Property 7: Rate Limit Response Headers",
        MaxTest = 100)]
    public Property Rate_limit_headers_reflect_configured_limit_and_remaining()
    {
        var permitLimitGen = Gen.Choose(5, 50);
        var gen = permitLimitGen.SelectMany(permitLimit =>
            Gen.Choose(1, permitLimit).Select(requestCount => (permitLimit, requestCount)));

        return Prop.ForAll(
            Arb.From(gen),
            testCase =>
            {
                var (permitLimit, requestCount) = testCase;

                using var host = CreateTestHost(permitLimit);
                var client = host.CreateClient();

                // Send requestCount requests and verify headers on the last one
                HttpResponseMessage? lastResponse = null;
                for (int i = 0; i < requestCount; i++)
                {
                    lastResponse?.Dispose();
                    lastResponse = client.GetAsync("/api/orders").GetAwaiter().GetResult();
                }

                try
                {
                    // Verify headers on the final response
                    var hasLimitHeader = lastResponse!.Headers.TryGetValues("X-RateLimit-Limit", out var limitValues);
                    var hasRemainingHeader = lastResponse.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues);

                    if (!hasLimitHeader || !hasRemainingHeader)
                    {
                        return false
                            .Label($"Missing headers — permitLimit={permitLimit}, requestCount={requestCount}, " +
                                   $"hasLimit={hasLimitHeader}, hasRemaining={hasRemainingHeader}");
                    }

                    var actualLimit = int.Parse(limitValues!.First());
                    var actualRemaining = int.Parse(remainingValues!.First());
                    var expectedRemaining = permitLimit - requestCount;

                    return (actualLimit == permitLimit && actualRemaining == expectedRemaining)
                        .Label($"permitLimit={permitLimit}, requestCount={requestCount}, " +
                               $"expectedLimit={permitLimit}, actualLimit={actualLimit}, " +
                               $"expectedRemaining={expectedRemaining}, actualRemaining={actualRemaining}");
                }
                finally
                {
                    lastResponse?.Dispose();
                }
            });
    }

    private static TestServer CreateTestHost(int permitLimit)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimit:PermitLimit"] = permitLimit.ToString(),
                ["RateLimit:WindowSeconds"] = "60"
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
                app.UseMiddleware<RateLimitHeadersMiddleware>();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/api/orders", () => "OK");
                });
            });

        return new TestServer(builder);
    }
}
