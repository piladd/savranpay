using BankTransfers.Application.Abstractions;
using BankTransfers.Domain.Risk;
using BankTransfers.Domain.Transfers;
using BankTransfers.Infrastructure.Demo;

namespace BankTransfers.Infrastructure.Compliance;

public sealed class AllowAllAmlService : IAmlService
{
    private readonly DemoBankStore _store;

    public AllowAllAmlService(DemoBankStore store)
    {
        _store = store;
    }

    public Task<AmlDecision> CheckAsync(TransferOrder transfer, CancellationToken cancellationToken)
    {
        var decision = transfer.Purpose.Contains("крипто", StringComparison.OrdinalIgnoreCase)
            ? AmlDecision.ManualReview
            : AmlDecision.Allowed;

        _store.RiskChecks.Add(new DemoRiskCheck(
            Guid.NewGuid(),
            transfer.Id,
            "AML",
            decision.ToString(),
            "Проверены идентификация клиента, назначение платежа и базовые риск-признаки.",
            DateTimeOffset.UtcNow));

        return Task.FromResult(decision);
    }
}
