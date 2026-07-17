namespace Orders.Api.Mcp;

/// <summary>
/// Categorises MCP tool results for cache TTL selection.
/// </summary>
public enum McpCacheCategory
{
    /// <summary>Reference/lookup data — long TTL (3600 s).</summary>
    ReferenceData,

    /// <summary>Entity state — short TTL (30 s).</summary>
    EntityState,

    /// <summary>Aggregation/report data — medium TTL (300 s).</summary>
    Aggregation
}
