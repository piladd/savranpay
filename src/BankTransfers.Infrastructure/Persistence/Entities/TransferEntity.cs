namespace BankTransfers.Infrastructure.Persistence.Entities;

public sealed class TransferEntity
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid FromAccountId { get; set; }
    public string RecipientType { get; set; } = string.Empty;
    public string RecipientAccountNumber { get; set; } = string.Empty;
    public string RecipientBankBic { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public long AmountMinorUnits { get; set; }
    public string Currency { get; set; } = "RUB";
    public string Purpose { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
