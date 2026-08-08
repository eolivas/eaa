using System.Net;
using System.Text;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Middleware;

namespace Orders.Api.Tests.Validation;

/// <summary>
/// Property-based tests for oversized payload rejection.
/// Validates: Requirements 19.4
/// </summary>
public class OversizedPayloadPropertyTests
{
    /// <summary>
    /// For any HTTP request with Content-Length greater than 1,048,576 bytes,
    /// the API returns HTTP 413 without deserializing the body.
    /// </summary>
    [Property(
        DisplayName = "Feature: template-architecture-gaps, Property 9: Oversized Payload Rejection",
        MaxTest = 100)]
    public Property Oversized_payload_returns_413()
    {
        return Prop.ForAll(
            GenerateOversizedContentLength(),
            contentLength =>
            {
                using var server = CreateTestServer();
                var client = server.CreateClient();

                // Create a request with Content-Length exceeding 1 MB
                var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders");
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                request.Content.Headers.ContentLength = contentLength;

                var response = client.SendAsync(request).GetAwaiter().GetResult();
                var statusCode = (int)response.StatusCode;

                return (statusCode == 413).Label(
                    $"Expected 413 but got {statusCode} for Content-Length={contentLength}");
            });
    }

    private static TestServer CreateTestServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseMiddleware<RequestBodySizeLimitMiddleware>();
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapPost("/api/orders", () => Results.Ok());
                });
            });

        return new TestServer(builder);
    }

    /// <summary>
    /// Generates Content-Length values greater than 1,048,576 (1 MB) up to 10,000,000.
    /// </summary>
    private static Arbitrary<long> GenerateOversizedContentLength()
    {
        var gen = Gen.Choose(1_048_577, 10_000_000)
            .Select(i => (long)i);
        return Arb.From(gen);
    }
}
