using SavranPay.Domain.Transfers;

namespace SavranPay.Application.Abstractions;

public interface ITransferExecutionService
{
    Task ExecuteAsync(TransferOrder transfer, CancellationToken cancellationToken);
}
