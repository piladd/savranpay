namespace SavranPay.Infrastructure.Persistence.Entities;

public sealed class OutboxMessageEntity
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
    public string? Error { get; set; }
}
