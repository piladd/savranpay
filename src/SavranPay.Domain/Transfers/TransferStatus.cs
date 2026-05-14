namespace SavranPay.Domain.Transfers;

public enum TransferStatus
{
    Draft = 0,
    PendingValidation = 1,
    PendingRiskCheck = 2,
    PendingClientConfirmation = 3,
    Accepted = 4,
    Reserved = 5,
    Processing = 6,
    Settled = 7,
    Failed = 8,
    Cancelled = 9,
    Reversed = 10,
    Disputed = 11
}
