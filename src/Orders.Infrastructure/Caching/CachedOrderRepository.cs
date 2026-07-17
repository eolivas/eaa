using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Orders.Domain;

namespace Orders.Infrastructure.Caching;

/// <summary>
/// Decorator that adds distributed caching to an <see cref="IOrderRepository"/> implementation.
/// Cache miss → inner repo → store with 5-minute absolute expiry.
/// Cache hit → return cached without calling inner repo.
/// </summary>
public sealed class CachedOrderRepository : IOrderRepository
{
    private readonly IOrderRepository _inner;
    private readonly IDistributedCache _cache;

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CachedOrderRepository(IOrderRepository inner, IDistributedCache cache)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"order:{id.Value}";

        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            var dto = JsonSerializer.Deserialize<OrderCacheDto>(cached, JsonOptions);
            return dto?.ToDomain();
        }

        var order = await _inner.GetByIdAsync(id, cancellationToken);
        if (order is not null)
        {
            var dto = OrderCacheDto.FromDomain(order);
            var json = JsonSerializer.Serialize(dto, JsonOptions);
            await _cache.SetStringAsync(cacheKey, json, CacheOptions, cancellationToken);
        }

        return order;
    }

    public Task<IReadOnlyList<Order>> GetByCustomerAsync(CustomerId customerId, CancellationToken cancellationToken = default)
    {
        return _inner.GetByCustomerAsync(customerId, cancellationToken);
    }

    public async Task SaveAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _inner.SaveAsync(order, cancellationToken);
        await _cache.RemoveAsync($"order:{order.Id.Value}", cancellationToken);
    }

    public async Task DeleteAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _inner.DeleteAsync(order, cancellationToken);
        await _cache.RemoveAsync($"order:{order.Id.Value}", cancellationToken);
    }

    /// <summary>
    /// Internal DTO for cache serialization of the Order aggregate.
    /// Required because Order uses private constructors and setters.
    /// </summary>
    private sealed class OrderCacheDto
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<OrderLineCacheDto> Lines { get; set; } = [];

        public static OrderCacheDto FromDomain(Order order)
        {
            return new OrderCacheDto
            {
                Id = order.Id.Value,
                CustomerId = order.CustomerId.Value,
                Status = order.Status.ToString(),
                Lines = order.Lines.Select(line => new OrderLineCacheDto
                {
                    Id = line.Id.Value,
                    ProductId = line.ProductId.Value,
                    Quantity = line.Quantity,
                    UnitPriceAmount = line.UnitPrice.Amount,
                    UnitPriceCurrency = line.UnitPrice.Currency
                }).ToList()
            };
        }

        public Order ToDomain()
        {
            var lines = Lines.Select(l =>
                OrderLine.Create(
                    new ProductId(l.ProductId),
                    l.Quantity,
                    new Money(l.UnitPriceAmount, l.UnitPriceCurrency)
                )).ToList();

            // Use Order.Create to build a valid aggregate, then reconcile the cached state.
            // Since Order.Create generates a new Id and always sets Pending status,
            // we use reflection to restore the cached identity and status.
            var order = Order.Create(new CustomerId(CustomerId), lines);

            SetPrivateProperty(order, nameof(Order.Id), new OrderId(Id));
            SetPrivateProperty(order, nameof(Order.Status), Enum.Parse<OrderStatus>(Status));

            // Restore original OrderLine IDs
            for (var i = 0; i < lines.Count && i < Lines.Count; i++)
            {
                SetPrivateProperty(lines[i], nameof(OrderLine.Id), new OrderLineId(Lines[i].Id));
            }

            // Clear domain events raised by Create (they are stale for a cached entity)
            order.ClearDomainEvents();

            return order;
        }

        private static void SetPrivateProperty<TEntity, TValue>(TEntity entity, string propertyName, TValue value)
        {
            var property = typeof(TEntity).GetProperty(propertyName);
            property?.SetValue(entity, value);
        }
    }

    private sealed class OrderLineCacheDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPriceAmount { get; set; }
        public string UnitPriceCurrency { get; set; } = string.Empty;
    }
}
