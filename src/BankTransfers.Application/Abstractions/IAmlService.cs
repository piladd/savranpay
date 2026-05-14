using BankTransfers.Domain.Risk;
using BankTransfers.Domain.Transfers;

namespace BankTransfers.Application.Abstractions;

public interface IAmlService
{
    Task<AmlDecision> CheckAsync(TransferOrder transfer, CancellationToken cancellationToken);
}
