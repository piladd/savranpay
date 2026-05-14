using BankTransfers.Application.Abstractions;
using BankTransfers.Domain.Transfers;
using BankTransfers.Domain.ValueObjects;
using BankTransfers.Infrastructure.Demo;

namespace BankTransfers.Infrastructure.Persistence;

public sealed class InMemoryTransferOrderRepository : ITransferOrderRepository
{
    private readonly DemoBankStore _store;

    public InMemoryTransferOrderRepository(DemoBankStore store)
    {
        _store = store;
    }

    public Task<TransferOrder?> GetByIdAsync(Guid transferId, CancellationToken cancellationToken)
    {
        _store.Transfers.TryGetValue(transferId, out var transfer);
        return Task.FromResult(transfer);
    }

    public Task<TransferOrder?> FindByIdempotencyKeyAsync(
        Guid customerId,
        IdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        var transfer = _store.Transfers.Values.FirstOrDefault(item =>
            item.CustomerId == customerId &&
            item.IdempotencyKey.Value == idempotencyKey.Value);

        return Task.FromResult(transfer);
    }

    public Task AddAsync(TransferOrder transfer, CancellationToken cancellationToken)
    {
        _store.Transfers[transfer.Id] = transfer;
        return Task.CompletedTask;
    }
}
