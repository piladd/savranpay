namespace BankTransfers.Api.Contracts;

public sealed record ConfirmTransferRequest(
    string ConfirmationType,
    string Signature,
    string Nonce,
    DateTimeOffset Timestamp);
