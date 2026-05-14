using SavranPay.Domain.Accounts;

namespace SavranPay.Application.Abstractions;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken);
}
