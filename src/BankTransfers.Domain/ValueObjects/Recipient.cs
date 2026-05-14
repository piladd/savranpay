namespace BankTransfers.Domain.ValueObjects;

public sealed record Recipient(
    string Type,
    string AccountNumber,
    string BankBic,
    string Name)
{
    public static Recipient Create(string type, string accountNumber, string bankBic, string name)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Recipient type is required.", nameof(type));
        }

        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            throw new ArgumentException("Recipient account number is required.", nameof(accountNumber));
        }

        if (string.IsNullOrWhiteSpace(bankBic))
        {
            throw new ArgumentException("Recipient bank BIC is required.", nameof(bankBic));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Recipient name is required.", nameof(name));
        }

        return new Recipient(type, accountNumber, bankBic, name);
    }
}
