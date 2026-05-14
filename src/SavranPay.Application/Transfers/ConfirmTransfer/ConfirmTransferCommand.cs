namespace SavranPay.Application.Transfers.ConfirmTransfer;

public sealed record ConfirmTransferCommand(
    Guid TransferId,
    string ConfirmationType,
    string Signature,
    string Nonce,
    DateTimeOffset Timestamp);
