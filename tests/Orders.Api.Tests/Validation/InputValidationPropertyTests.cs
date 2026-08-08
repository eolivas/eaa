using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Endpoints;
using Orders.Api.Middleware;
using Orders.Application.Behaviours;
using Orders.Application.Commands;
using Orders.Domain;

namespace Orders.Api.Tests.Validation;

/// <summary>
/// Property-based tests for input validation numeric constraints on order lines.
/// Validates: Requirements 19.1, 19.2
/// </summary>
public class InputValidationPropertyTests
{
    /// <summary>
    /// For any order line with Quantity &lt; 1 or UnitPrice &lt;= 0,
    /// the API returns HTTP 400 with a ProblemDetails response containing
    /// an "errors" dictionary keyed by property name.
    /// </summary>
    [Property(
        DisplayName = "Feature: template-architecture-gaps, Property 8: Input Validation — Order Line Numeric Constraints",
        MaxTest = 100)]
    public Property Invalid_numeric_constraints_return_400_with_errors()
    {
        return Prop.ForAll(
            GenerateInvalidQuantity(),
            GenerateInvalidUnitPrice(),
            (invalidQuantity, invalidUnitPrice) =>
            {
                using var server = CreateTestServer();
                var client = server.CreateClient();

                var requestBody = new
                {
                    customerId = Guid.NewGuid(),
                    lines = new[]
                    {
                        new
                        {
                            productId = Guid.NewGuid(),
                            quantity = invalidQuantity,
                            unitPrice = invalidUnitPrice,
                            currency = "USD"
                        }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = client.PostAsync("/api/orders", content).GetAwaiter().GetResult();
                var statusCode = (int)response.StatusCode;

                if (statusCode != 400)
                {
                    return false.Label(
                        $"Expected 400 but got {statusCode} " +
                        $"(quantity={invalidQuantity}, unitPrice={invalidUnitPrice})");
                }

                var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                // Verify ProblemDetails has "errors" dictionary
                if (!root.TryGetProperty("errors", out var errorsElement))
                {
                    return false.Label(
                        $"Response missing 'errors' dictionary " +
                        $"(quantity={invalidQuantity}, unitPrice={invalidUnitPrice}). " +
                        $"Body: {responseBody}");
                }

                // Verify errors dictionary has entries (keyed by property name)
                var hasEntries = errorsElement.EnumerateObject().Any();

                return hasEntries.Label(
                    $"Errors dictionary is empty " +
                    $"(quantity={invalidQuantity}, unitPrice={invalidUnitPrice}). " +
                    $"Body: {responseBody}");
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
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    // Map POST /api/orders without authorization (bypasses auth for testing)
                    endpoints.MapPost("/api/orders", async (PlaceOrderRequest request, ISender sender) =>
                    {
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
                        return Results.Created($"/api/orders/{id.Value}", new { id = id.Value });
                    });
                });
            });

        return new TestServer(builder);
    }

    /// <summary>
    /// Generates invalid quantities: integers in range [-100, 0] (all fail Quantity >= 1).
    /// </summary>
    private static Arbitrary<int> GenerateInvalidQuantity()
    {
        var gen = Gen.Choose(-100, 0);
        return Arb.From(gen);
    }

    /// <summary>
    /// Generates invalid unit prices: decimals in range [-100.0, 0.0] (all fail UnitPrice > 0).
    /// </summary>
    private static Arbitrary<decimal> GenerateInvalidUnitPrice()
    {
        // Generate a double in [-100.0, 0.0] and convert to decimal
        var gen = Gen.Choose(-10000, 0)
            .Select(i => (decimal)i / 100m);
        return Arb.From(gen);
    }
}
