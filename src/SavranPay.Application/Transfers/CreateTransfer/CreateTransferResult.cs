using SavranPay.Domain.Transfers;

namespace SavranPay.Application.Transfers.CreateTransfer;

public sealed record CreateTransferResult(Guid TransferId, TransferStatus Status);
