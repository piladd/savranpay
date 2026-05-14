namespace SavranPay.Domain.ValueObjects;

public sealed record Money
{
    public long MinorUnits { get; }
    public string Currency { get; }

    public Money(long minorUnits, string currency)
    {
        if (minorUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minorUnits), "Amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        MinorUnits = minorUnits;
        Currency = currency.ToUpperInvariant();
    }

    public static Money Rub(long minorUnits) => new(minorUnits, "RUB");

    public static Money Zero(string currency) => new(0, currency);
}
