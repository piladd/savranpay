using BankTransfers.Domain.Transfers;

namespace BankTransfers.Application.Abstractions;

public interface ITransferExecutionService
{
    Task ExecuteAsync(TransferOrder transfer, CancellationToken cancellationToken);
}
