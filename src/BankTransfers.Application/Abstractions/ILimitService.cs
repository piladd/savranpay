using BankTransfers.Domain.Transfers;

namespace BankTransfers.Application.Abstractions;

public interface ILimitService
{
    Task EnsureTransferAllowedAsync(TransferOrder transfer, CancellationToken cancellationToken);
}
