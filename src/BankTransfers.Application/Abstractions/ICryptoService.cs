using BankTransfers.Domain.Transfers;

namespace BankTransfers.Application.Abstractions;

public interface ICryptoService
{
    string CreateTransferPayload(TransferOrder transfer, string nonce, DateTimeOffset timestamp);

    string ComputePayloadHash(string payload);

    Task<bool> VerifyTransferSignatureAsync(
        TransferOrder transfer,
        string signature,
        string nonce,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken);
}
