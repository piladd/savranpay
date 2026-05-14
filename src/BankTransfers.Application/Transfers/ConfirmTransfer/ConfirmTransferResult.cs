using BankTransfers.Domain.Transfers;

namespace BankTransfers.Application.Transfers.ConfirmTransfer;

public sealed record ConfirmTransferResult(Guid TransferId, TransferStatus Status);
