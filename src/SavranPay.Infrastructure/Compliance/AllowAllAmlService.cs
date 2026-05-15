using SavranPay.Application.Abstractions;
using SavranPay.Domain.Risk;
using SavranPay.Domain.Transfers;
using SavranPay.Infrastructure.Demo;

namespace SavranPay.Infrastructure.Compliance;

public sealed class AllowAllAmlService : IAmlService
{
    private readonly DemoBankStore _store;

    public AllowAllAmlService(DemoBankStore store)
    {
        _store = store;
    }

    public Task<AmlDecision> CheckAsync(TransferOrder transfer, CancellationToken cancellationToken)
    {
        var decision = transfer.Purpose.Contains("crypto", StringComparison.OrdinalIgnoreCase) ||
                       transfer.Purpose.Contains("крипто", StringComparison.OrdinalIgnoreCase)
            ? AmlDecision.ManualReview
            : AmlDecision.Allowed;

        lock (_store.SyncRoot)
        {
            _store.RiskChecks.Add(new DemoRiskCheck(
                Guid.NewGuid(),
                transfer.Id,
                "AML",
                decision.ToString(),
                "Customer identification, payment purpose and baseline AML indicators were checked.",
                DateTimeOffset.UtcNow));
        }

        return Task.FromResult(decision);
    }
}
