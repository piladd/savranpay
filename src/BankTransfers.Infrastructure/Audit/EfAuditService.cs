using BankTransfers.Application.Abstractions;
using BankTransfers.Infrastructure.Persistence;
using BankTransfers.Infrastructure.Persistence.Entities;
using System.Text.Json;

namespace BankTransfers.Infrastructure.Audit;

public sealed class EfAuditService : IAuditService
{
    private readonly SavranPayDbContext _db;

    public EfAuditService(SavranPayDbContext db)
    {
        _db = db;
    }

    public Task WriteAsync(Guid operationId, string eventType, string message, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        _db.AuditEvents.Add(new AuditEventEntity
        {
            Id = Guid.NewGuid(),
            OperationId = operationId,
            EventType = eventType,
            Message = message,
            CreatedAt = now
        });

        _db.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            Type = $"Audit.{eventType}",
            Payload = JsonSerializer.Serialize(new { operationId, eventType, message }),
            CreatedAt = now
        });

        return Task.CompletedTask;
    }
}
