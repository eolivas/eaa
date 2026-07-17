using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Orders.Api.Mcp;

/// <summary>
/// OpenTelemetry instrumentation for MCP tool call token usage and cost tracking.
/// </summary>
public class McpTokenInstrumentation
{
    private static readonly Meter Meter = new("Orders.Mcp");
    private static readonly Counter<long> InputTokensCounter = Meter.CreateCounter<long>("mcp.tokens.input", "tokens", "Input tokens consumed by MCP tool calls");
    private static readonly Counter<long> OutputTokensCounter = Meter.CreateCounter<long>("mcp.tokens.output", "tokens", "Output tokens produced by MCP tool calls");

    /// <summary>
    /// Records token usage and cost for a completed MCP tool call.
    /// Increments mcp.tokens.input and mcp.tokens.output counters,
    /// and sets span tags for tokens and estimated cost.
    /// </summary>
    /// <param name="toolName">The name of the MCP tool that was called.</param>
    /// <param name="modelTier">The model tier used (Lightweight, Standard, Heavy).</param>
    /// <param name="inputTokens">Estimated input token count.</param>
    /// <param name="outputTokens">Estimated output token count.</param>
    /// <param name="costUsd">Estimated cost in USD.</param>
    public void RecordToolCallMetrics(string toolName, string modelTier, long inputTokens, long outputTokens, decimal costUsd)
    {
        var tags = new TagList
        {
            { "tool.name", toolName },
            { "model.tier", modelTier }
        };

        InputTokensCounter.Add(inputTokens, tags);
        OutputTokensCounter.Add(outputTokens, tags);

        // Set span tags on the current activity
        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetTag("mcp.tokens.input", inputTokens);
            activity.SetTag("mcp.tokens.output", outputTokens);
            activity.SetTag("mcp.cost.usd", (double)costUsd);
        }
    }

    /// <summary>
    /// Estimates token count from a string (approximate: chars / 4).
    /// </summary>
    public static long EstimateTokens(string text)
    {
        return text.Length / 4;
    }
}
