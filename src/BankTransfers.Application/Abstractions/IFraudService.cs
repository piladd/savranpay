using BankTransfers.Domain.Risk;
using BankTransfers.Domain.Transfers;

namespace BankTransfers.Application.Abstractions;

public interface IFraudService
{
    Task<FraudDecision> CheckAsync(TransferOrder transfer, CancellationToken cancellationToken);
}
