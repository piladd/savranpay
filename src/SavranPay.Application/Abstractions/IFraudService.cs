using SavranPay.Domain.Risk;
using SavranPay.Domain.Transfers;

namespace SavranPay.Application.Abstractions;

public interface IFraudService
{
    Task<FraudDecision> CheckAsync(TransferOrder transfer, CancellationToken cancellationToken);
}
