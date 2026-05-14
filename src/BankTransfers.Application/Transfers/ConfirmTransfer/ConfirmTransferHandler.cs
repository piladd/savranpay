using BankTransfers.Application.Abstractions;
using BankTransfers.SharedKernel;

namespace BankTransfers.Application.Transfers.ConfirmTransfer;

public sealed class ConfirmTransferHandler
{
    private readonly ITransferOrderRepository _transfers;
    private readonly ICryptoService _crypto;
    private readonly ITransferExecutionService _execution;
    private readonly IAuditService _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ConfirmTransferHandler(
        ITransferOrderRepository transfers,
        ICryptoService crypto,
        ITransferExecutionService execution,
        IAuditService audit,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _transfers = transfers;
        _crypto = crypto;
        _execution = execution;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<ConfirmTransferResult> Handle(ConfirmTransferCommand command, CancellationToken cancellationToken)
    {
        var transfer = await _transfers.GetByIdAsync(command.TransferId, cancellationToken)
            ?? throw new InvalidOperationException("Transfer was not found.");

        var signatureValid = await _crypto.VerifyTransferSignatureAsync(
            transfer,
            command.Signature,
            command.Nonce,
            command.Timestamp,
            cancellationToken);

        if (!signatureValid)
        {
            transfer.Fail(_clock.UtcNow);
            await _transfers.AddAsync(transfer, cancellationToken);
            await _audit.WriteAsync(transfer.Id, "ConfirmationFailed", "Invalid transfer signature.", cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ConfirmTransferResult(transfer.Id, transfer.Status);
        }

        transfer.Confirm(_clock.UtcNow);
        transfer.Reserve(_clock.UtcNow);
        transfer.StartProcessing(_clock.UtcNow);
        await _execution.ExecuteAsync(transfer, cancellationToken);
        transfer.Settle(_clock.UtcNow);

        await _transfers.AddAsync(transfer, cancellationToken);
        await _audit.WriteAsync(transfer.Id, "TransferConfirmed", "Client confirmation accepted.", cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ConfirmTransferResult(transfer.Id, transfer.Status);
    }
}
