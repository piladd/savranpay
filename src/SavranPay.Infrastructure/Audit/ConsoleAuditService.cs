using SavranPay.Application.Abstractions;
using SavranPay.Infrastructure.Demo;

namespace SavranPay.Infrastructure.Audit;

public sealed class ConsoleAuditService : IAuditService
{
    private readonly DemoBankStore _store;

    public ConsoleAuditService(DemoBankStore store)
    {
        _store = store;
    }

    public Task WriteAsync(Guid operationId, string eventType, string message, CancellationToken cancellationToken)
    {
        _store.AuditEvents.Add(new DemoAuditEvent(
            Guid.NewGuid(),
            operationId,
            eventType,
            message,
            DateTimeOffset.UtcNow));

        Console.WriteLine($"{DateTimeOffset.UtcNow:o} Operation={operationId} Event={eventType} Message={message}");

        return Task.CompletedTask;
    }
}
