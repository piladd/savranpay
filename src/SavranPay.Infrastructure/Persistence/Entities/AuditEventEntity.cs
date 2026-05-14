namespace SavranPay.Infrastructure.Persistence.Entities;

public sealed class AuditEventEntity
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
