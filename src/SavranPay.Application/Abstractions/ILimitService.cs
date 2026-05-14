using SavranPay.Domain.Transfers;

namespace SavranPay.Application.Abstractions;

public interface ILimitService
{
    Task EnsureTransferAllowedAsync(TransferOrder transfer, CancellationToken cancellationToken);
}
