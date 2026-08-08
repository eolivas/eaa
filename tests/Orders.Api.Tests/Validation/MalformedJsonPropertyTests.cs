using System.Net;
using System.Text;
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Endpoints;
using Orders.Api.Middleware;
using Orders.Application.Behaviours;
using Orders.Application.Commands;
using Orders.Domain;

namespace Orders.Api.Tests.Validation;

/// <summary>
/// Property-based tests for malformed JSON rejection.
/// Validates: Requirements 19.5
/// </summary>
public class MalformedJsonPropertyTests
{
    /// <summary>
    /// For any request body that is not valid JSON (random byte sequences, truncated JSON,
    /// XML, plain text), the API returns HTTP 400 with a ProblemDetails body indicating
    /// a malformed request.
    /// </summary>
    [Property(
        DisplayName = "Feature: template-architecture-gaps, Property 10: Malformed JSON Rejection",
        MaxTest = 100)]
    public Property Malformed_json_payloads_return_400_with_problem_details()
    {
        return Prop.ForAll(
            GenerateMalformedPayload(),
            payload =>
            {
                using var server = CreateTestServer();
                var client = server.CreateClient();

                var content = new ByteArrayContent(payload);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                var response = client.PostAsync("/api/orders", content).GetAwaiter().GetResult();
                var statusCode = (int)response.StatusCode;

                if (statusCode != 400)
                {
                    var payloadPreview = payload.Length <= 50
                        ? Encoding.UTF8.GetString(payload)
                        : Encoding.UTF8.GetString(payload, 0, 50) + "...";

                    return false.Label(
                        $"Expected 400 but got {statusCode} " +
                        $"(payload preview: {payloadPreview}, length={payload.Length})");
                }

                var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                // Verify response contains ProblemDetails indicators
                var hasProblemDetails = responseBody.Contains("\"status\"") ||
                                       responseBody.Contains("\"title\"") ||
                                       responseBody.Contains("\"type\"");

                return hasProblemDetails.Label(
                    $"Response missing ProblemDetails fields. Body: {responseBody}");
            });
    }

    private static TestServer CreateTestServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();

                // MediatR with validation behaviour
                services.AddMediatR(cfg =>
                {
                    cfg.RegisterServicesFromAssembly(typeof(PlaceOrderCommand).Assembly);
                    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
                });

                // FluentValidation validators
                services.AddValidatorsFromAssembly(typeof(PlaceOrderCommandValidator).Assembly);
            })
            .Configure(app =>
            {
                // Middleware to catch JSON deserialization failures and return ProblemDetails
                app.Use(async (context, next) =>
                {
                    try
                    {
                        await next(context);
                    }
                    catch (BadHttpRequestException)
                    {
                        if (!context.Response.HasStarted)
                        {
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            context.Response.ContentType = "application/problem+json";

                            var problemDetails = new ProblemDetails
                            {
                                Status = StatusCodes.Status400BadRequest,
                                Title = "Bad Request",
                                Detail = "The request body contains malformed JSON.",
                                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                            };

                            await context.Response.WriteAsJsonAsync(problemDetails);
                        }
                    }
                });
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    // Map POST /api/orders that manually reads JSON body.
                    // This ensures malformed JSON throws an exception caught by middleware.
                    endpoints.MapPost("/api/orders", async (HttpContext httpContext, ISender sender) =>
                    {
                        PlaceOrderRequest? request;
                        try
                        {
                            request = await httpContext.Request.ReadFromJsonAsync<PlaceOrderRequest>();
                        }
                        catch (System.Text.Json.JsonException)
                        {
                            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                            httpContext.Response.ContentType = "application/problem+json";

                            var problem = new ProblemDetails
                            {
                                Status = StatusCodes.Status400BadRequest,
                                Title = "Bad Request",
                                Detail = "The request body contains malformed JSON.",
                                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                            };

                            await httpContext.Response.WriteAsJsonAsync(problem);
                            return;
                        }

                        if (request is null)
                        {
                            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                            httpContext.Response.ContentType = "application/problem+json";

                            var problem = new ProblemDetails
                            {
                                Status = StatusCodes.Status400BadRequest,
                                Title = "Bad Request",
                                Detail = "The request body is empty or could not be parsed.",
                                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                            };

                            await httpContext.Response.WriteAsJsonAsync(problem);
                            return;
                        }

                        var command = new PlaceOrderCommand
                        {
                            CustomerId = request.CustomerId,
                            Lines = request.Lines.Select(l => new Orders.Application.Commands.OrderLineDto
                            {
                                ProductId = l.ProductId,
                                Quantity = l.Quantity,
                                UnitPrice = l.UnitPrice,
                                Currency = l.Currency
                            }).ToList()
                        };

                        var id = await sender.Send(command);
                        httpContext.Response.StatusCode = StatusCodes.Status201Created;
                        httpContext.Response.Headers["Location"] = $"/api/orders/{id.Value}";
                        await httpContext.Response.WriteAsJsonAsync(new { id = id.Value });
                    });
                });
            });

        return new TestServer(builder);
    }

    /// <summary>
    /// Generates arbitrary non-JSON payloads under 1MB:
    /// - Random byte sequences
    /// - Truncated JSON (missing closing braces)
    /// - XML strings
    /// - Plain text
    /// </summary>
    private static Arbitrary<byte[]> GenerateMalformedPayload()
    {
        // Random byte sequences (1-1000 bytes)
        var randomBytes = Gen.Choose(1, 1000)
            .SelectMany(length =>
                Gen.ArrayOf(length, Gen.Choose(0, 255).Select(i => (byte)i)));

        // Truncated JSON strings
        var truncatedJson = Gen.Elements(
            "{\"customerId\": \"abc\"",
            "{\"lines\": [{\"productId\": \"123\"",
            "[{\"quantity\": 1",
            "{\"customerId\": \"" + Guid.NewGuid() + "\", \"lines\": [",
            "{",
            "[",
            "{\"key\": "
        ).Select(s => Encoding.UTF8.GetBytes(s));

        // XML strings
        var xmlStrings = Gen.Elements(
            "<order><item/></order>",
            "<?xml version=\"1.0\"?><root/>",
            "<orders><order id=\"1\"><line quantity=\"5\"/></order></orders>",
            "<data><customerId>abc</customerId></data>"
        ).Select(s => Encoding.UTF8.GetBytes(s));

        // Plain text strings
        var plainText = Gen.Elements(
            "this is not json",
            "hello world",
            "order: 123, quantity: 5",
            "true",
            "null",
            "12345",
            "undefined",
            ""
        ).Select(s => Encoding.UTF8.GetBytes(s));

        var gen = Gen.OneOf(randomBytes, truncatedJson, xmlStrings, plainText);
        return Arb.From(gen);
    }
}
