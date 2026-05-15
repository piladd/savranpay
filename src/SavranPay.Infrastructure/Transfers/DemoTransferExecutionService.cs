using SavranPay.Application.Abstractions;
using SavranPay.Infrastructure.Demo;

namespace SavranPay.Infrastructure.Transfers;

public sealed class DemoTransferExecutionService : ITransferExecutionService
{
    private readonly DemoBankStore _store;

    public DemoTransferExecutionService(DemoBankStore store)
    {
        _store = store;
    }

    public Task ExecuteAsync(Domain.Transfers.TransferOrder transfer, CancellationToken cancellationToken)
    {
        lock (_store.SyncRoot)
        {
            var source = _store.Accounts[transfer.FromAccountId];
            source.Debit(transfer.Amount);

            var recipient = _store.Accounts.Values.FirstOrDefault(account =>
                account.Number == transfer.Recipient.AccountNumber &&
                account.Id != source.Id);

            recipient?.Credit(transfer.Amount);
            var now = DateTimeOffset.UtcNow;

            _store.LedgerEntries.Add(new DemoLedgerEntry(
                Guid.NewGuid(),
                transfer.Id,
                source.Number,
                transfer.Amount.MinorUnits,
                0,
                transfer.Amount.Currency,
                now));

            _store.LedgerEntries.Add(new DemoLedgerEntry(
                Guid.NewGuid(),
                transfer.Id,
                recipient?.Number ?? transfer.Recipient.AccountNumber,
                0,
                transfer.Amount.MinorUnits,
                transfer.Amount.Currency,
                now));

            _store.Notifications.Add(new DemoNotification(
                Guid.NewGuid(),
                transfer.Id,
                "Push",
                _store.Customer.Phone,
                "Sent",
                now));
        }

        return Task.CompletedTask;
    }
}
