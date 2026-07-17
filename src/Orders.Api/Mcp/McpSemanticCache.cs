using System.Security.Cryptography;
using System.Text;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Orders.Api.Mcp;

/// <summary>
/// Semantic cache for MCP tool results.
/// Keys are computed as the SHA-256 hash of toolName + serialised arguments.
/// On cache hit the <c>mcp.cache.hits</c> counter is incremented and no token counters are updated.
/// </summary>
public class McpSemanticCache
{
    private readonly IDistributedCache _cache;
    private readonly McpSemanticCacheOptions _options;
    private readonly Counter<long> _cacheHitsCounter;

    public McpSemanticCache(
        IDistributedCache cache,
        IOptions<McpSemanticCacheOptions> options,
        Counter<long> cacheHitsCounter)
    {
        _cache = cache;
        _options = options.Value;
        _cacheHitsCounter = cacheHitsCounter;
    }

    /// <summary>
    /// Computes a deterministic cache key from the tool name and its serialised arguments
    /// using SHA-256 hashing.
    /// </summary>
    public string ComputeCacheKey(string toolName, string serializedArguments)
    {
        var input = toolName + serializedArguments;
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Attempts to retrieve a cached result for the given tool call.
    /// On cache hit, increments the <c>mcp.cache.hits</c> counter.
    /// </summary>
    /// <returns>The cached result string, or <c>null</c> on cache miss.</returns>
    public async Task<string?> GetAsync(string toolName, string serializedArguments, CancellationToken ct = default)
    {
        var key = ComputeCacheKey(toolName, serializedArguments);
        var cached = await _cache.GetStringAsync(key, ct);

        if (cached is not null)
        {
            _cacheHitsCounter.Add(1, new KeyValuePair<string, object?>("tool.name", toolName));
        }

        return cached;
    }

    /// <summary>
    /// Stores a tool result in the cache with a TTL determined by the specified category.
    /// </summary>
    public async Task SetAsync(
        string toolName,
        string serializedArguments,
        string result,
        McpCacheCategory category,
        CancellationToken ct = default)
    {
        var key = ComputeCacheKey(toolName, serializedArguments);
        var ttl = GetTtl(category);

        var entryOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttl)
        };

        await _cache.SetStringAsync(key, result, entryOptions, ct);
    }

    private int GetTtl(McpCacheCategory category) => category switch
    {
        McpCacheCategory.ReferenceData => _options.ReferenceDataTtlSeconds,
        McpCacheCategory.EntityState => _options.EntityStateTtlSeconds,
        McpCacheCategory.Aggregation => _options.AggregationTtlSeconds,
        _ => _options.EntityStateTtlSeconds
    };
}
