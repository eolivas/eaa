using System.Text.RegularExpressions;

namespace Orders.Domain;

/// <summary>
/// Immutable value object representing a monetary amount with currency.
/// Uses record semantics for structural equality.
/// </summary>
public partial record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        if (!IsoCurrencyRegex().IsMatch(currency))
            throw new ArgumentException(
                "Currency must be a valid ISO 4217 code (three uppercase letters).",
                nameof(currency));

        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Creates a Money instance with zero amount for the specified currency.
    /// </summary>
    public static Money Zero(string currency) => new(0m, currency);

    public static Money operator +(Money left, Money right)
    {
        if (left.Currency != right.Currency)
            throw new InvalidOperationException(
                $"Cannot add Money with different currencies: '{left.Currency}' and '{right.Currency}'.");

        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator *(Money money, int quantity)
        => new(money.Amount * quantity, money.Currency);

    public static Money operator *(Money money, decimal factor)
        => new(money.Amount * factor, money.Currency);

    public override string ToString() => $"{Amount} {Currency}";

    [GeneratedRegex(@"^[A-Z]{3}$")]
    private static partial Regex IsoCurrencyRegex();
}
