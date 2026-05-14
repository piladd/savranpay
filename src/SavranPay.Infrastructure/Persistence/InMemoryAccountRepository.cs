using SavranPay.Application.Abstractions;
using SavranPay.Domain.Accounts;
using SavranPay.Infrastructure.Demo;

namespace SavranPay.Infrastructure.Persistence;

public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly DemoBankStore _store;

    public InMemoryAccountRepository(DemoBankStore store)
    {
        _store = store;
    }

    public static Guid DemoCustomerId => DemoBankStore.DemoCustomerId;
    public static Guid DemoAccountId => DemoBankStore.PrimaryAccountId;

    public Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken)
    {
        _store.Accounts.TryGetValue(accountId, out var account);
        return Task.FromResult(account);
    }
}
