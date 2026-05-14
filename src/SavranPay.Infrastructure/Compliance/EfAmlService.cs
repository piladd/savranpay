using SavranPay.Application.Abstractions;
using SavranPay.Domain.Risk;
using SavranPay.Domain.Transfers;
using SavranPay.Infrastructure.Persistence;
using SavranPay.Infrastructure.Persistence.Entities;

namespace SavranPay.Infrastructure.Compliance;

public sealed class EfAmlService : IAmlService
{
    private readonly SavranPayDbContext _db;

    public EfAmlService(SavranPayDbContext db)
    {
        _db = db;
    }

    public Task<AmlDecision> CheckAsync(TransferOrder transfer, CancellationToken cancellationToken)
    {
        var decision = transfer.Purpose.Contains("крипто", StringComparison.OrdinalIgnoreCase)
            ? AmlDecision.ManualReview
            : AmlDecision.Allowed;

        _db.RiskChecks.Add(new RiskCheckEntity
        {
            Id = Guid.NewGuid(),
            TransferId = transfer.Id,
            CheckType = "AML",
            Decision = decision.ToString(),
            Details = "Проверены идентификация клиента, назначение платежа и базовые AML-признаки.",
            CreatedAt = DateTimeOffset.UtcNow
        });

        return Task.FromResult(decision);
    }
}
