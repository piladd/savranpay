using BankTransfers.Application.Abstractions;
using BankTransfers.Infrastructure.Demo;

namespace BankTransfers.Infrastructure.Transfers;

public sealed class DemoTransferExecutionService : ITransferExecutionService
{
    private readonly DemoBankStore _store;

    public DemoTransferExecutionService(DemoBankStore store)
    {
        _store = store;
    }

    public Task ExecuteAsync(Domain.Transfers.TransferOrder transfer, CancellationToken cancellationToken)
    {
        var source = _store.Accounts[transfer.FromAccountId];
        source.Debit(transfer.Amount);

        var recipient = _store.Accounts.Values.FirstOrDefault(account =>
            account.Number == transfer.Recipient.AccountNumber &&
            account.Id != source.Id);

        recipient?.Credit(transfer.Amount);

        _store.LedgerEntries.Add(new DemoLedgerEntry(
            Guid.NewGuid(),
            transfer.Id,
            source.Number,
            transfer.Amount.MinorUnits,
            0,
            transfer.Amount.Currency,
            DateTimeOffset.UtcNow));

        _store.LedgerEntries.Add(new DemoLedgerEntry(
            Guid.NewGuid(),
            transfer.Id,
            recipient?.Number ?? transfer.Recipient.AccountNumber,
            0,
            transfer.Amount.MinorUnits,
            transfer.Amount.Currency,
            DateTimeOffset.UtcNow));

        _store.Notifications.Add(new DemoNotification(
            Guid.NewGuid(),
            transfer.Id,
            "Push",
            _store.Customer.Phone,
            "Sent",
            DateTimeOffset.UtcNow));

        return Task.CompletedTask;
    }
}
