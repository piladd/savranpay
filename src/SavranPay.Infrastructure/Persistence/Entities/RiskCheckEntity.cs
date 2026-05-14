namespace SavranPay.Infrastructure.Persistence.Entities;

public sealed class RiskCheckEntity
{
    public Guid Id { get; set; }
    public Guid TransferId { get; set; }
    public string CheckType { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
