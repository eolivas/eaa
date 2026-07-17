using System.ComponentModel;
using System.Text.Json;
using MediatR;
using ModelContextProtocol.Server;
using Orders.Application.Commands;
using Orders.Application.Queries;
using Orders.Domain.Exceptions;
using CommandOrderLineDto = Orders.Application.Commands.OrderLineDto;

namespace Orders.Api.Mcp;

/// <summary>
/// MCP (Model Context Protocol) tools exposed by the Orders API.
/// Provides get_order and place_order capabilities for AI agents.
/// </summary>
[McpServerToolType]
public class OrderMcpTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Retrieves an order by its identifier.
    /// Returns the serialised order DTO or a not-found message.
    /// </summary>
    [McpServerTool(Name = "get_order"), Description("Retrieves an order by its ID.")]
    public static async Task<string> GetOrder(
        ISender sender,
        [Description("The order ID (UUID).")] string orderId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(orderId, out var parsedId))
        {
            return $"No order found with ID {orderId}.";
        }

        var order = await sender.Send(new GetOrderQuery(parsedId), cancellationToken);

        if (order is null)
        {
            return $"No order found with ID {orderId}.";
        }

        return JsonSerializer.Serialize(order, JsonOptions);
    }

    /// <summary>
    /// Places a new order for a customer.
    /// Deserialises the lines JSON, dispatches PlaceOrderCommand via MediatR,
    /// and returns the new order ID on success.
    /// </summary>
    [McpServerTool(Name = "place_order"), Description("Places a new order for a customer.")]
    public static async Task<string> PlaceOrder(
        ISender sender,
        [Description("The customer ID (UUID).")] string customerId,
        [Description("JSON array of order lines. Each line: {\"productId\": \"uuid\", \"quantity\": int, \"unitPrice\": decimal, \"currency\": \"string\"}.")] string linesJson,
        CancellationToken cancellationToken)
    {
        // Parse customerId
        if (!Guid.TryParse(customerId, out var parsedCustomerId))
        {
            return "Invalid customerId format. Expected a valid UUID.";
        }

        // Deserialise order lines
        List<CommandOrderLineDto>? lines;
        try
        {
            lines = JsonSerializer.Deserialize<List<CommandOrderLineDto>>(linesJson, JsonOptions);

            if (lines is null || lines.Count == 0)
            {
                return "Invalid linesJson: expected a non-empty JSON array of order lines.";
            }
        }
        catch (JsonException ex)
        {
            return $"Invalid linesJson format: {ex.Message}";
        }

        // Dispatch command
        try
        {
            var command = new PlaceOrderCommand
            {
                CustomerId = parsedCustomerId,
                Lines = lines.AsReadOnly()
            };

            var orderId = await sender.Send(command, cancellationToken);
            return $"Order placed successfully. Order ID: {orderId.Value}";
        }
        catch (OrderDomainException ex)
        {
            return ex.Message;
        }
    }
}
