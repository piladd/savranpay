using SavranPay.Application.Abstractions;

namespace SavranPay.Infrastructure.Persistence;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly SavranPayDbContext _db;

    public EfUnitOfWork(SavranPayDbContext db)
    {
        _db = db;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}
