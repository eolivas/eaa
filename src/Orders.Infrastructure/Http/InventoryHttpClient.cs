using System.Net;

namespace Orders.Infrastructure.Http;

/// <summary>
/// Typed HTTP client for communicating with an external service.
/// Demonstrates: Resilient HTTP client pattern with retry and circuit-breaker via AddStandardResilienceHandler.
/// Replace the endpoint and logic with your domain-specific external service call.
/// </summary>
public sealed class InventoryHttpClient
{
    private readonly HttpClient _httpClient;

    public InventoryHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Example external service call. Replace with your domain-specific operation.
    /// </summary>
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
                "External service is unavailable. All retries have been exhausted.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ServiceUnavailableException(
                "External service is unavailable. Request timed out after all retries.", ex);
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
