namespace BankTransfers.Infrastructure.Persistence.Entities;

public sealed class NotificationEntity
{
    public Guid Id { get; set; }
    public Guid TransferId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
