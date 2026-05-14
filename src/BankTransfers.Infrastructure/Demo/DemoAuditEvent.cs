namespace BankTransfers.Infrastructure.Demo;

public sealed record DemoAuditEvent(
    Guid Id,
    Guid OperationId,
    string EventType,
    string Message,
    DateTimeOffset CreatedAt);
