using System.Net;

namespace Orders.Infrastructure.Http;

/// <summary>
/// Typed HTTP client for communicating with the Inventory service.
/// Registered via AddHttpClient with AddStandardResilienceHandler for retry and circuit-breaker behaviour.
/// When all retries are exhausted, throws <see cref="ServiceUnavailableException"/> indicating HTTP 503.
/// </summary>
public sealed class InventoryHttpClient
{
    private readonly HttpClient _httpClient;

    public InventoryHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Checks inventory availability for the specified product.
    /// </summary>
    /// <param name="productId">The product identifier to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the product is available in inventory; otherwise false.</returns>
    /// <exception cref="ServiceUnavailableException">
    /// Thrown when the Inventory service is unreachable after all retries are exhausted.
    /// </exception>
    public async Task<bool> CheckInventoryAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/api/inventory/{productId}/availability",
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (HttpRequestException ex)
        {
            throw new ServiceUnavailableException(
                "Inventory service is unavailable. All retries have been exhausted.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout scenario — the resilience handler's timeout fired
            throw new ServiceUnavailableException(
                "Inventory service is unavailable. Request timed out after all retries.", ex);
        }
    }
}

/// <summary>
/// Exception thrown when a downstream service is unavailable after all retries are exhausted.
/// Maps to HTTP 503 Service Unavailable at the presentation layer.
/// </summary>
public sealed class ServiceUnavailableException : Exception
{
    public ServiceUnavailableException(string message)
        : base(message)
    {
    }

    public ServiceUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
