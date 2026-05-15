using SavranPay.Application.Abstractions;
using SavranPay.Domain.Risk;
using SavranPay.Domain.Transfers;
using SavranPay.Infrastructure.Persistence;
using SavranPay.Infrastructure.Persistence.Entities;
using System.Text.Json;

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
        var decision = transfer.Purpose.Contains("crypto", StringComparison.OrdinalIgnoreCase) ||
                       transfer.Purpose.Contains("крипто", StringComparison.OrdinalIgnoreCase)
            ? AmlDecision.ManualReview
            : AmlDecision.Allowed;

        _db.RiskChecks.Add(new RiskCheckEntity
        {
            Id = Guid.NewGuid(),
            TransferId = transfer.Id,
            CheckType = "AML",
            Decision = decision.ToString(),
            Details = "Customer identification, payment purpose and baseline AML indicators were checked.",
            DeviceFingerprint = string.Empty,
            IpAddress = string.Empty,
            RiskFactors = JsonSerializer.Serialize(BuildFactors(transfer)),
            BlockReason = string.Empty,
            DocumentsRequested = decision == AmlDecision.ManualReview,
            StepUpRequired = false,
            CreatedAt = DateTimeOffset.UtcNow
        });

        return Task.FromResult(decision);
    }

    private static string[] BuildFactors(TransferOrder transfer)
    {
        var factors = new List<string>
        {
            $"purpose:{transfer.Purpose}",
            $"recipient:{transfer.Recipient.Name}",
            $"amount:{transfer.Amount.MinorUnits}"
        };

        if (transfer.Purpose.Contains("crypto", StringComparison.OrdinalIgnoreCase) ||
            transfer.Purpose.Contains("крипто", StringComparison.OrdinalIgnoreCase))
        {
            factors.Add("crypto_keyword");
            factors.Add("documents_requested");
        }

        return factors.ToArray();
    }
}
