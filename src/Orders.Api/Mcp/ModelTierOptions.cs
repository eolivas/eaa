namespace Orders.Api.Mcp;

/// <summary>
/// Configuration options for mapping MCP tools to model tiers.
/// </summary>
public sealed class ModelTierOptions
{
    /// <summary>
    /// Maps tool names to their assigned model tier.
    /// </summary>
    public Dictionary<string, ModelTier> ToolTierMap { get; set; } = new();

    /// <summary>
    /// The default tier used when no explicit mapping exists for a tool.
    /// </summary>
    public ModelTier DefaultTier { get; set; } = ModelTier.Lightweight;
}
