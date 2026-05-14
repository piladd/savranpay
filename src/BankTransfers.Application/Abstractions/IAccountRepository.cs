using BankTransfers.Domain.Accounts;

namespace BankTransfers.Application.Abstractions;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid accountId, CancellationToken cancellationToken);
}
