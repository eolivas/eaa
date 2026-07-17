using Bogus;
using Orders.Domain;

namespace Orders.Domain.Tests;

/// <summary>
/// Test-data builder that generates valid <see cref="Order"/> aggregates using Bogus.
/// Invariants guaranteed:
///   - Non-null OrderId
///   - At least one OrderLine
///   - Total.Amount ≥ 0
/// </summary>
public sealed class OrderFaker
{
    private static readonly string[] IsoCurrencies = ["USD", "EUR", "GBP", "JPY", "CAD", "AUD", "CHF"];

    private readonly Faker _faker = new();
    private OrderStatus _targetStatus = OrderStatus.Pending;
    private int? _lineCount;
    private string? _currency;

    /// <summary>
    /// Configure the faker to produce orders in <see cref="OrderStatus.Placed"/> state.
    /// </summary>
    public OrderFaker WithPlacedStatus()
    {
        _targetStatus = OrderStatus.Placed;
        return this;
    }

    /// <summary>
    /// Configure the faker to produce orders in <see cref="OrderStatus.Pending"/> state (default).
    /// </summary>
    public OrderFaker WithPendingStatus()
    {
        _targetStatus = OrderStatus.Pending;
        return this;
    }

    /// <summary>
    /// Configure the number of order lines to generate (minimum 1).
    /// </summary>
    public OrderFaker WithLineCount(int count)
    {
        _lineCount = Math.Max(1, count);
        return this;
    }

    /// <summary>
    /// Configure a specific currency for all generated Money values.
    /// </summary>
    public OrderFaker WithCurrency(string currency)
    {
        _currency = currency;
        return this;
    }

    /// <summary>
    /// Generates a single valid <see cref="Order"/> aggregate.
    /// </summary>
    public Order Generate()
    {
        var currency = _currency ?? _faker.PickRandom(IsoCurrencies);
        var lineCount = _lineCount ?? _faker.Random.Int(1, 5);

        var lines = Enumerable.Range(0, lineCount)
            .Select(_ => GenerateOrderLine(currency))
            .ToList()
            .AsReadOnly();

        var customerId = CustomerId.New();
        var order = Order.Create(customerId, lines);

        if (_targetStatus == OrderStatus.Placed)
        {
            order.Place();
        }

        return order;
    }

    /// <summary>
    /// Generates multiple valid <see cref="Order"/> aggregates.
    /// </summary>
    public List<Order> Generate(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => Generate())
            .ToList();
    }

    private OrderLine GenerateOrderLine(string currency)
    {
        var productId = ProductId.New();
        var quantity = _faker.Random.Int(1, 100);
        var amount = _faker.Finance.Amount(0.01m, 9999.99m);
        var unitPrice = new Money(amount, currency);

        return OrderLine.Create(productId, quantity, unitPrice);
    }
}
