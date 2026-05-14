namespace SavranPay.Application.Abstractions;

public interface IAuditService
{
    Task WriteAsync(Guid operationId, string eventType, string message, CancellationToken cancellationToken);
}
