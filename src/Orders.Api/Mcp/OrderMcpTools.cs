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
/// MCP (Model Context Protocol) tools exposed by the API.
/// Provides get and create capabilities for AI agents.
/// Replace with your domain-specific MCP tools.
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
    /// Retrieves a resource by its identifier.
    /// </summary>
    [McpServerTool(Name = "get_order"), Description("Retrieves a resource by its ID.")]
    public static async Task<string> GetOrder(
        ISender sender,
        [Description("The resource ID (UUID).")] string orderId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(orderId, out var parsedId))
        {
            return $"No resource found with ID {orderId}.";
        }

        var order = await sender.Send(new GetOrderQuery(parsedId), cancellationToken);

        if (order is null)
        {
            return $"No resource found with ID {orderId}.";
        }

        return JsonSerializer.Serialize(order, JsonOptions);
    }

    /// <summary>
    /// Creates a new resource.
    /// </summary>
    [McpServerTool(Name = "place_order"), Description("Creates a new resource.")]
    public static async Task<string> PlaceOrder(
        ISender sender,
        [Description("The owner/customer ID (UUID).")] string customerId,
        [Description("JSON array of line items. Each item: {\"productId\": \"uuid\", \"quantity\": int, \"unitPrice\": decimal, \"currency\": \"string\"}.")] string linesJson,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(customerId, out var parsedCustomerId))
        {
            return "Invalid customerId format. Expected a valid UUID.";
        }

        List<CommandOrderLineDto>? lines;
        try
        {
            lines = JsonSerializer.Deserialize<List<CommandOrderLineDto>>(linesJson, JsonOptions);

            if (lines is null || lines.Count == 0)
            {
                return "Invalid linesJson: expected a non-empty JSON array of line items.";
            }
        }
        catch (JsonException ex)
        {
            return $"Invalid linesJson format: {ex.Message}";
        }

        try
        {
            var command = new PlaceOrderCommand
            {
                CustomerId = parsedCustomerId,
                Lines = lines.AsReadOnly()
            };

            var orderId = await sender.Send(command, cancellationToken);
            return $"Resource created successfully. ID: {orderId.Value}";
        }
        catch (OrderDomainException ex)
        {
            return ex.Message;
        }
    }
}
