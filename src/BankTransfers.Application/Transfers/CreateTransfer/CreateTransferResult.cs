using BankTransfers.Domain.Transfers;

namespace BankTransfers.Application.Transfers.CreateTransfer;

public sealed record CreateTransferResult(Guid TransferId, TransferStatus Status);
