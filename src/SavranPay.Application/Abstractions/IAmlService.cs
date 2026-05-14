using SavranPay.Domain.Risk;
using SavranPay.Domain.Transfers;

namespace SavranPay.Application.Abstractions;

public interface IAmlService
{
    Task<AmlDecision> CheckAsync(TransferOrder transfer, CancellationToken cancellationToken);
}
