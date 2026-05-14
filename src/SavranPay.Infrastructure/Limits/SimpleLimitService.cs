using SavranPay.Application.Abstractions;
using SavranPay.Domain.Transfers;

namespace SavranPay.Infrastructure.Limits;

public sealed class SimpleLimitService : ILimitService
{
    private const long MaxSingleTransferMinorUnits = 600_000_00;

    public Task EnsureTransferAllowedAsync(TransferOrder transfer, CancellationToken cancellationToken)
    {
        if (transfer.Amount.MinorUnits > MaxSingleTransferMinorUnits)
        {
            throw new InvalidOperationException("Transfer exceeds the single operation limit.");
        }

        return Task.CompletedTask;
    }
}
