namespace SavranPay.Infrastructure.Persistence.Entities;

public sealed class RiskCheckEntity
{
    public Guid Id { get; set; }
    public Guid TransferId { get; set; }
    public string CheckType { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string DeviceFingerprint { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string RiskFactors { get; set; } = "[]";
    public string BlockReason { get; set; } = string.Empty;
    public bool DocumentsRequested { get; set; }
    public bool StepUpRequired { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
