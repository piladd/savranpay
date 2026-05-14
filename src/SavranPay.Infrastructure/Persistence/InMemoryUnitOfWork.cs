using SavranPay.Application.Abstractions;

namespace SavranPay.Infrastructure.Persistence;

public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
