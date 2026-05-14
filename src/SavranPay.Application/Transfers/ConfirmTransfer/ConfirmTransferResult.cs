using SavranPay.Domain.Transfers;

namespace SavranPay.Application.Transfers.ConfirmTransfer;

public sealed record ConfirmTransferResult(Guid TransferId, TransferStatus Status);
