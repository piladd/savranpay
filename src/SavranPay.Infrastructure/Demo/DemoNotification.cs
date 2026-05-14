namespace SavranPay.Infrastructure.Demo;

public sealed record DemoNotification(
    Guid Id,
    Guid TransferId,
    string Channel,
    string Recipient,
    string Status,
    DateTimeOffset CreatedAt);
