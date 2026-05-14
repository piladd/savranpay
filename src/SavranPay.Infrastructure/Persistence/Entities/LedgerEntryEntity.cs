namespace SavranPay.Infrastructure.Persistence.Entities;

public sealed class LedgerEntryEntity
{
    public Guid Id { get; set; }
    public Guid TransferId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public long DebitMinorUnits { get; set; }
    public long CreditMinorUnits { get; set; }
    public string Currency { get; set; } = "RUB";
    public DateTimeOffset CreatedAt { get; set; }
}
