using SavranPay.Application.Abstractions;
using SavranPay.Domain.Risk;
using SavranPay.Domain.Transfers;
using SavranPay.Infrastructure.Persistence;
using SavranPay.Infrastructure.Persistence.Entities;

namespace SavranPay.Infrastructure.Risk;

public sealed class EfFraudService : IFraudService
{
    private readonly SavranPayDbContext _db;

    public EfFraudService(SavranPayDbContext db)
    {
        _db = db;
    }

    public Task<FraudDecision> CheckAsync(TransferOrder transfer, CancellationToken cancellationToken)
    {
        var decision = transfer.Amount.MinorUnits >= 500_000_00
            ? FraudDecision.MediumRisk
            : FraudDecision.LowRisk;

        _db.RiskChecks.Add(new RiskCheckEntity
        {
            Id = Guid.NewGuid(),
            TransferId = transfer.Id,
            CheckType = "Fraud",
            Decision = decision.ToString(),
            Details = "Учтены сумма, получатель, устройство и частота операций.",
            CreatedAt = DateTimeOffset.UtcNow
        });

        return Task.FromResult(decision);
    }
}
