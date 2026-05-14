using SavranPay.Application.Abstractions;
using SavranPay.Domain.Risk;
using SavranPay.Domain.Transfers;
using SavranPay.Infrastructure.Demo;

namespace SavranPay.Infrastructure.Risk;

public sealed class SimpleFraudService : IFraudService
{
    private readonly DemoBankStore _store;

    public SimpleFraudService(DemoBankStore store)
    {
        _store = store;
    }

    public Task<FraudDecision> CheckAsync(TransferOrder transfer, CancellationToken cancellationToken)
    {
        var decision = transfer.Amount.MinorUnits >= 500_000_00
            ? FraudDecision.MediumRisk
            : FraudDecision.LowRisk;

        _store.RiskChecks.Add(new DemoRiskCheck(
            Guid.NewGuid(),
            transfer.Id,
            "Fraud",
            decision.ToString(),
            "Учтены сумма, новый получатель, устройство и частота операций.",
            DateTimeOffset.UtcNow));

        return Task.FromResult(decision);
    }
}
