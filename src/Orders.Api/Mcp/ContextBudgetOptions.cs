namespace Orders.Api.Mcp;

/// <summary>
/// Configuration options for per-tool-call context budget enforcement.
/// Budget breakdown: system prompt + tool schemas + history + result + margin = total.
/// </summary>
public sealed class ContextBudgetOptions
{
    public int SystemPromptTokens { get; set; } = 500;
    public int ToolSchemaTokens { get; set; } = 500;
    public int HistoryTokens { get; set; } = 2000;
    public int ResultTokens { get; set; } = 4000;
    public int MarginTokens { get; set; } = 1000;

    /// <summary>
    /// Computed total budget (sum of all token allocations).
    /// </summary>
    public int TotalBudget => SystemPromptTokens + ToolSchemaTokens + HistoryTokens + ResultTokens + MarginTokens;

    /// <summary>
    /// Message appended when a result payload is truncated at the budget boundary.
    /// </summary>
    public string TruncationMessage { get; set; } =
        "\n[...truncated: result exceeded context budget. Request a smaller page.]";
}
