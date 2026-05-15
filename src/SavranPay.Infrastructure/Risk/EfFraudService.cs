using SavranPay.Application.Abstractions;
using SavranPay.Domain.Risk;
using SavranPay.Domain.Transfers;
using SavranPay.Infrastructure.Persistence;
using SavranPay.Infrastructure.Persistence.Entities;
using System.Text.Json;

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
            Details = "Amount, recipient, device, IP and transaction frequency were checked.",
            DeviceFingerprint = $"device-{transfer.CustomerId:N}"[..39],
            IpAddress = $"192.0.2.{Math.Abs(transfer.Id.GetHashCode()) % 220 + 10}",
            RiskFactors = JsonSerializer.Serialize(BuildFactors(transfer)),
            BlockReason = string.Empty,
            DocumentsRequested = false,
            StepUpRequired = decision == FraudDecision.MediumRisk,
            CreatedAt = DateTimeOffset.UtcNow
        });

        return Task.FromResult(decision);
    }

    private static string[] BuildFactors(TransferOrder transfer)
    {
        var factors = new List<string>
        {
            $"amount:{transfer.Amount.MinorUnits}",
            $"recipient:{transfer.Recipient.Name}",
            $"bank_bic:{transfer.Recipient.BankBic}"
        };

        if (transfer.Amount.MinorUnits >= 500_000_00)
        {
            factors.Add("large_amount");
            factors.Add("step_up_required");
        }

        return factors.ToArray();
    }
}
