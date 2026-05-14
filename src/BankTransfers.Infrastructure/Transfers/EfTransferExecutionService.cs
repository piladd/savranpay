using BankTransfers.Application.Abstractions;
using BankTransfers.Infrastructure.Persistence;
using BankTransfers.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BankTransfers.Infrastructure.Transfers;

public sealed class EfTransferExecutionService : ITransferExecutionService
{
    private readonly SavranPayDbContext _db;

    public EfTransferExecutionService(SavranPayDbContext db)
    {
        _db = db;
    }

    public async Task ExecuteAsync(Domain.Transfers.TransferOrder transfer, CancellationToken cancellationToken)
    {
        var source = await _db.Accounts.SingleAsync(item => item.Id == transfer.FromAccountId, cancellationToken);
        source.AvailableMinorUnits -= transfer.Amount.MinorUnits;

        var recipient = await _db.Accounts.FirstOrDefaultAsync(
            item => item.Number == transfer.Recipient.AccountNumber && item.Id != source.Id,
            cancellationToken);

        if (recipient is not null)
        {
            recipient.AvailableMinorUnits += transfer.Amount.MinorUnits;
        }

        var now = DateTimeOffset.UtcNow;
        _db.Ledger.AddRange(
            new LedgerEntryEntity
            {
                Id = Guid.NewGuid(),
                TransferId = transfer.Id,
                AccountNumber = source.Number,
                DebitMinorUnits = transfer.Amount.MinorUnits,
                CreditMinorUnits = 0,
                Currency = transfer.Amount.Currency,
                CreatedAt = now
            },
            new LedgerEntryEntity
            {
                Id = Guid.NewGuid(),
                TransferId = transfer.Id,
                AccountNumber = recipient?.Number ?? transfer.Recipient.AccountNumber,
                DebitMinorUnits = 0,
                CreditMinorUnits = transfer.Amount.MinorUnits,
                Currency = transfer.Amount.Currency,
                CreatedAt = now
            });

        _db.Notifications.Add(new NotificationEntity
        {
            Id = Guid.NewGuid(),
            TransferId = transfer.Id,
            Channel = "Push",
            Recipient = "customer",
            Status = "Queued",
            CreatedAt = now
        });

        _db.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = Guid.NewGuid(),
            Type = "Notification.TransferSettled",
            Payload = JsonSerializer.Serialize(new { transferId = transfer.Id, channel = "Push", status = "Queued" }),
            CreatedAt = now
        });
    }
}
