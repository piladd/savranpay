namespace BankTransfers.Infrastructure.Persistence.Entities;

public sealed class AccountEntity
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Number { get; set; } = string.Empty;
    public long AvailableMinorUnits { get; set; }
    public long ReservedMinorUnits { get; set; }
    public string Currency { get; set; } = "RUB";
    public string Status { get; set; } = string.Empty;
}
