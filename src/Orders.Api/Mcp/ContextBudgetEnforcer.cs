using Microsoft.Extensions.Options;

namespace Orders.Api.Mcp;

/// <summary>
/// Enforces context budget constraints on MCP tool results and provides
/// model-tier resolution for tools.
/// </summary>
public sealed class ContextBudgetEnforcer
{
    private readonly ContextBudgetOptions _budgetOptions;
    private readonly ModelTierOptions _tierOptions;

    public ContextBudgetEnforcer(
        IOptions<ContextBudgetOptions> budgetOptions,
        IOptions<ModelTierOptions> tierOptions)
    {
        _budgetOptions = budgetOptions.Value;
        _tierOptions = tierOptions.Value;
    }

    /// <summary>
    /// Enforces the result token budget on a tool result payload.
    /// Uses approximate token estimation (chars / 4).
    /// If the estimated token count exceeds the result budget, truncates at the
    /// character boundary and appends the configured truncation message.
    /// </summary>
    public string EnforceResultBudget(string result)
    {
        var estimatedTokens = result.Length / 4;

        if (estimatedTokens <= _budgetOptions.ResultTokens)
        {
            return result;
        }

        var maxChars = _budgetOptions.ResultTokens * 4;
        return string.Concat(result.AsSpan(0, maxChars), _budgetOptions.TruncationMessage);
    }

    /// <summary>
    /// Resolves the model tier for a given tool name using the configured tier map.
    /// Falls back to the configured default tier (Lightweight) when no mapping exists.
    /// </summary>
    public ModelTier GetTierForTool(string toolName)
    {
        return _tierOptions.ToolTierMap.TryGetValue(toolName, out var tier)
            ? tier
            : _tierOptions.DefaultTier;
    }
}
