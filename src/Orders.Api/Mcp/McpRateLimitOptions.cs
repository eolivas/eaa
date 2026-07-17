namespace Orders.Api.Mcp;

/// <summary>
/// Configuration options for the MCP gateway fixed-window rate limiter.
/// </summary>
public class McpRateLimitOptions
{
    public int PermitLimit { get; set; } = 50;
    public int WindowSeconds { get; set; } = 3600; // 1 hour
    public int QueueLimit { get; set; } = 5;
    public int QueueTimeoutSeconds { get; set; } = 30;
}
