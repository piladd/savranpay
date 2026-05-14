namespace BankTransfers.Domain.ValueObjects;

public sealed record IdempotencyKey
{
    public string Value { get; }

    public IdempotencyKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Idempotency key is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
