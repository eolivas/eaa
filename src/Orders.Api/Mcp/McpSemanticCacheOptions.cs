namespace Orders.Api.Mcp;

/// <summary>
/// Configuration options for the MCP semantic cache TTLs (in seconds).
/// Bind to the "Mcp:SemanticCache" configuration section.
/// </summary>
public class McpSemanticCacheOptions
{
    /// <summary>TTL for reference data tool results (default 3600 s).</summary>
    public int ReferenceDataTtlSeconds { get; set; } = 3600;

    /// <summary>TTL for entity state tool results (default 30 s).</summary>
    public int EntityStateTtlSeconds { get; set; } = 30;

    /// <summary>TTL for aggregation tool results (default 300 s).</summary>
    public int AggregationTtlSeconds { get; set; } = 300;
}
