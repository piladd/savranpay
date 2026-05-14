using BankTransfers.Application.Abstractions;
using BankTransfers.Domain.Accounts;
using BankTransfers.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BankTransfers.Infrastructure.Persistence;

public sealed class EfAccountRepository : IAccountRepository
{
    private readonly SavranPayDbContext _db;

    public EfAccountRepository(SavranPayDbContext db)
    {
        _db = db;
    }

    public async Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var entity = await _db.Accounts.AsNoTracking().SingleOrDefaultAsync(item => item.Id == accountId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return Account.Rehydrate(
            entity.Id,
            entity.CustomerId,
            entity.Number,
            new Money(entity.AvailableMinorUnits, entity.Currency),
            new Money(entity.ReservedMinorUnits, entity.Currency),
            Enum.Parse<AccountStatus>(entity.Status));
    }
}
