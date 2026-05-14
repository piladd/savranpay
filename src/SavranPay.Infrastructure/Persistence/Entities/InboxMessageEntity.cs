namespace SavranPay.Infrastructure.Persistence.Entities;

public sealed class InboxMessageEntity
{
    public Guid Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
