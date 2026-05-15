using SavranPay.Application.Abstractions;
using SavranPay.Domain.Transfers;
using SavranPay.Domain.ValueObjects;
using SavranPay.Infrastructure.Demo;

namespace SavranPay.Infrastructure.Persistence;

public sealed class InMemoryTransferOrderRepository : ITransferOrderRepository
{
    private readonly DemoBankStore _store;

    public InMemoryTransferOrderRepository(DemoBankStore store)
    {
        _store = store;
    }

    public Task<TransferOrder?> GetByIdAsync(Guid transferId, CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            _store.Transfers.TryGetValue(transferId, out var transfer);
            return Task.FromResult(transfer);
        }
    }

    public Task<TransferOrder?> FindByIdempotencyKeyAsync(
        Guid customerId,
        IdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        TransferOrder? transfer;
        lock (_store.SyncRoot)
        {
            transfer = _store.Transfers.Values.FirstOrDefault(item =>
                item.CustomerId == customerId &&
                item.IdempotencyKey.Value == idempotencyKey.Value);
        }

        return Task.FromResult(transfer);
    }

    public Task AddAsync(TransferOrder transfer, CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            _store.Transfers[transfer.Id] = transfer;
        }

        return Task.CompletedTask;
    }
}
