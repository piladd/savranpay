using BankTransfers.Domain.Transfers;
using BankTransfers.Domain.ValueObjects;

namespace BankTransfers.Application.Abstractions;

public interface ITransferOrderRepository
{
    Task<TransferOrder?> GetByIdAsync(Guid transferId, CancellationToken cancellationToken);

    Task<TransferOrder?> FindByIdempotencyKeyAsync(
        Guid customerId,
        IdempotencyKey idempotencyKey,
        CancellationToken cancellationToken);

    Task AddAsync(TransferOrder transfer, CancellationToken cancellationToken);
}
