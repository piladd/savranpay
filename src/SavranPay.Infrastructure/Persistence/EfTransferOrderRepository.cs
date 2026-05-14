using SavranPay.Application.Abstractions;
using SavranPay.Domain.Transfers;
using SavranPay.Domain.ValueObjects;
using SavranPay.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace SavranPay.Infrastructure.Persistence;

public sealed class EfTransferOrderRepository : ITransferOrderRepository
{
    private readonly SavranPayDbContext _db;

    public EfTransferOrderRepository(SavranPayDbContext db)
    {
        _db = db;
    }

    public async Task<TransferOrder?> GetByIdAsync(Guid transferId, CancellationToken cancellationToken)
    {
        var entity = await _db.Transfers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == transferId, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<TransferOrder?> FindByIdempotencyKeyAsync(
        Guid customerId,
        IdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        var entity = await _db.Transfers.AsNoTracking().SingleOrDefaultAsync(
            item => item.CustomerId == customerId && item.IdempotencyKey == idempotencyKey.Value,
            cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(TransferOrder transfer, CancellationToken cancellationToken)
    {
        var entity = await _db.Transfers.SingleOrDefaultAsync(item => item.Id == transfer.Id, cancellationToken);
        if (entity is null)
        {
            _db.Transfers.Add(ToEntity(transfer));
            return;
        }

        Apply(transfer, entity);
    }

    private static TransferOrder ToDomain(TransferEntity entity)
    {
        return TransferOrder.Rehydrate(
            entity.Id,
            entity.CustomerId,
            entity.FromAccountId,
            Recipient.Create(entity.RecipientType, entity.RecipientAccountNumber, entity.RecipientBankBic, entity.RecipientName),
            new Money(entity.AmountMinorUnits, entity.Currency),
            entity.Purpose,
            new IdempotencyKey(entity.IdempotencyKey),
            Enum.Parse<TransferStatus>(entity.Status),
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static TransferEntity ToEntity(TransferOrder transfer)
    {
        var entity = new TransferEntity();
        Apply(transfer, entity);
        return entity;
    }

    private static void Apply(TransferOrder transfer, TransferEntity entity)
    {
        entity.Id = transfer.Id;
        entity.CustomerId = transfer.CustomerId;
        entity.FromAccountId = transfer.FromAccountId;
        entity.RecipientType = transfer.Recipient.Type;
        entity.RecipientAccountNumber = transfer.Recipient.AccountNumber;
        entity.RecipientBankBic = transfer.Recipient.BankBic;
        entity.RecipientName = transfer.Recipient.Name;
        entity.AmountMinorUnits = transfer.Amount.MinorUnits;
        entity.Currency = transfer.Amount.Currency;
        entity.Purpose = transfer.Purpose;
        entity.IdempotencyKey = transfer.IdempotencyKey.Value;
        entity.Status = transfer.Status.ToString();
        entity.CreatedAt = transfer.CreatedAt;
        entity.UpdatedAt = transfer.UpdatedAt;
    }
}
